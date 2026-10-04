# Notas de crédito (cliente y proveedor) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Agregar los documentos Nota de Crédito Cliente (objeto 8) y Nota de Crédito Proveedor (objeto 15), con tablas, numeración, API, permisos y pantallas Web, sin efecto en inventario y con límite sobre la factura base.

**Architecture:** La pila de notas de débito (ya implementada y revisada) se **clona** con sustituciones mecánicas de nombre (`Debito`→`Credito`), y solo las piezas que cambian de comportamiento se escriben completas: dominio (totales calculados por el servidor, misma moneda, límite acreditable dentro de una transacción `Serializable`), contratos de factura base con saldo acreditable, aplicación y los parches de JS/vista de la Web.

**Tech Stack:** .NET 7 API (EF Core 7, AutoMapper, xUnit + Moq), .NET 8 MVC Web (jQuery, DataTables, Bootstrap, SweetAlert2), SQL Server, bash/sed/node para las derivaciones.

**Spec:** `docs/superpowers/specs/2026-10-04-notas-credito-design.md` (repo API). Documento hermano ya implementado: `docs/superpowers/specs/2026-10-04-notas-debito-design.md`.

## Rutas y estado de partida

- API: `C:\Users\migue\source\repos\angelm0508\API`, rama `desarrollo`, HEAD `c5fb963` (spec). Línea base de pruebas: **1137** pasando.
- Web: `C:\Users\migue\source\repos\angelm0508\Web`, rama `main`, HEAD `42d374b`.
- Build/test con `BaseOutputPath` en el scratchpad (ejecutar los de la API **desde PowerShell**; en bash la barra final rompe el quoting):
  - API: `dotnet test API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apitest\"`
  - Web: `dotnet build Web.slnx -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\webbuild\"`

## Global Constraints

Cada tarea las incluye implícitamente.

- **Sin inventario:** una nota de crédito nunca llama a `IInventarioAsientoService` ni toca existencias; sus líneas no tienen `CodArticulo` ni `CodAlmacen`.
- **Tipos de objeto:** Nota de Crédito Cliente = `8`, Nota de Crédito Proveedor = `15`; `NumeracionDocumento.SubTipoDoc = '--'`; `DocAlias` ≤ 20 caracteres (`Nota Credito Cliente` = 20, `Nota Credito Prov.` = 18).
- **Tablas:** `NotaCredito`, `NotaCreditoDetalle`, `NotaCreditoCompra`, `NotaCreditoCompraDetalle`; constraints con prefijo `nota_credito` (`pk_nota_credito`, `fk_nota_credito_serie`, `fk_nota_credito_sn`, `fk_nota_credito_moneda`, `pk_nota_credito_det`, `fk_nota_credito_det_cod_impuesto` y los `_compra`).
- **Módulos de permisos:** `NotasCredito` (grupo `Ventas`, nombre `Notas de crédito`) y `NotasCreditoCompra` (grupo `Compras`, nombre `Notas de crédito de compra`). Sin filas en `UsuarioPermiso` no hay acceso; `SuperUsuario` se salta la matriz.
- **Factura base:** `BaseTipo` ∈ {`-1`, `6` (cliente) / `13` (proveedor)}; debe existir, no estar cancelada, ser del mismo socio y de **la misma moneda**; solo referencia (no se modifica ni se cierra). Con base: `Σ TotalDoc de las notas de crédito no canceladas con ese BaseEntry` + `TotalDoc` de la nota ≤ `TotalDoc` de la factura, comparado a 2 decimales, **dentro de la transacción `Serializable`** (`IEjecutorTransaccion.EjecutarAsync(op, System.Data.IsolationLevel.Serializable)`). Sin base: sin límite.
- **Totales calculados por el servidor** (a diferencia de la nota de débito): por línea `bruto = Cantidad × Precio`, `descuento = bruto × PrctjeDesc / 100`, `subtotal = bruto − descuento`, `Impuesto = subtotal × Impuesto.Tasa / 100`, `TotalLinea = subtotal + Impuesto`, `PrecioBruto = bruto`; cada monto se redondea a 2 decimales con `Math.Round(x, 2, MidpointRounding.AwayFromZero)`; el encabezado: `TotalBruto = Σ bruto`, `TotalDesc = Σ descuento`, `TotalImp = Σ Impuesto`, `TotalDoc = Σ TotalLinea`. Lo que mande el cliente se ignora.
- **Cancelar** (`PUT` con `Cancelado == "S"`) exige además `Accion.Eliminar` (`PermisoUsuario.Tiene` en API, `User.TienePermiso` en Web). **Eliminar** exige haber cancelado antes. Líneas inmutables tras crear el documento.
- **Permisos de los endpoints (iguales a notas de débito):** `GET {id}`, `GET`, `GET {id}/Lineas` = `Ver`; `GET FacturasBase/{codigoSn}` y `GET FacturaBase/{entry}` (proveedor `FacturasCompraBase` / `FacturaCompraBase`) = `Agregar`; `POST` = `Agregar`; `PUT` = `Actualizar` (+ `Eliminar` si cancela); `DELETE` = `Eliminar`. En la Web, `BuscarFacturasBase`/`ObtenerFacturaBase` = `Agregar`; `[ValidateAntiForgeryToken]` en todo POST.
- **Mensajes en español exactamente como aparecen** en los bloques de código del plan (los tests los verifican literalmente).
- **Git:** NUNCA `git add -A` ni `git add .`; siempre rutas explícitas. `API.Service.WebApi/appsettings.json` puede aparecer modificado localmente: NUNCA commitearlo. La Web tiene trabajo ajeno sin commitear (`Web.ApiClient/Dtos/Articulo/ArticuloCrearDTO.cs`, `Web.ApiClient/Dtos/Respuesta.cs`, `Web.UI/Controllers/ArticulosController.cs`, `Web.UI/wwwroot/js/articulos.js`): jamás se stagean, commitean ni modifican. Sin push hasta aprobación final del usuario. Los archivos derivados se escriben **solo** donde indica cada tarea; nunca se sobrescribe un archivo ya commiteado por error (las derivaciones escriben archivos nuevos `NotaCredito*`).
- **Trailer de commit exacto:** `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
- **Estilo/EOL:** se conserva el fin de línea de cada archivo existente que se edita (CRLF/LF; el script `.cs` y `.cshtml` nuevos en LF); sin churn de EOL.

## La derivación `nc` (se repite en cada tarea que la usa)

Casi todo el código de notas de crédito es la nota de débito con otro nombre. La función bash `nc` (Git Bash, desde la raíz del repo correspondiente) clona un archivo de nota de débito aplicando solo la sustitución de palabras; cada tarea aclara qué literales extra hay que ajustar a mano en los archivos derivados:

```bash
nc() {   # $1 = archivo de nota de debito (origen)   $2 = archivo de nota de credito (destino, NUEVO)
  [ -e "$2" ] && { echo "El destino ya existe: $2"; return 1; }
  sed -E \
    -e 's/Débito/Crédito/g' -e 's/débito/crédito/g' \
    -e 's/Debito/Credito/g' -e 's/debito/credito/g' \
    "$1" > "$2"
}
```

(`NotaDebito`→`NotaCredito`, `NotasDebito`→`NotasCredito`, `notaDebito`→`notaCredito`, `nota_debito`→`nota_credito`, `notasdebito`→`notascredito` y los textos "nota de débito"→"nota de crédito" quedan cubiertos por esas cuatro reglas.)

## File Structure

**API (repo `API`):**
- Crear `sql/2026-10-05-notas-credito.sql`; modificar `sql/crear-api-db-test-completo.sql`.
- Crear entidades `NotaCredito.cs`, `NotaCreditoDetalle.cs`, `NotaCreditoCompra.cs`, `NotaCreditoCompraDetalle.cs`; modificar `ApiDbTestContext.cs`, `SocioNegocio.cs`, `Monedum.cs`, `NumeracionDocumentoDet.cs`, `Impuesto.cs`.
- Crear repos `NotaCredito{,Detalle,Compra,CompraDetalle}Repositorio.cs`.
- Crear DTOs en `API.Application.DTO/notaCredito/` y `notaCreditoCompra/`; `API.Domain.Interface/{FacturaBaseCredito,INotaCreditoDomain,INotaCreditoCompraDomain}.cs`; `API.Application.Interface/{INotaCreditoApplication,INotaCreditoCompraApplication}.cs`.
- Crear `API.Domain.Core/{NotaCreditoDomain,NotaCreditoCompraDomain}.cs`; `API.Application.Main/{NotaCreditoApplication,NotaCreditoCompraApplication}.cs`; `API.Service.WebApi/Controllers/{NotaCreditoController,NotaCreditoCompraController}.cs`.
- Modificar `API.Transversal.Mapper/PerfilMapeo.cs`, `API.Service.WebApi/Startup.cs`.
- Pruebas en `API.Service.WebApi.Tests/{Domain,Application,Controllers}/`.

**Web (repo `Web`):**
- Crear `Web.ApiClient/Dtos/NotaCredito/*.cs`, `Web.ApiClient/Dtos/NotaCreditoCompra/*.cs`, `Web.ApiClient/Clientes/{INotaCreditoApiClient,NotaCreditoApiClient,INotaCreditoCompraApiClient,NotaCreditoCompraApiClient}.cs`; modificar `Web.UI/Program.cs`.
- Crear `Web.UI/Controllers/{NotasCreditoController,NotasCreditoCompraController}.cs`, `Web.UI/Views/NotasCredito/{Index,_Form}.cshtml`, `Web.UI/Views/NotasCreditoCompra/{Index,_Form}.cshtml`, `Web.UI/wwwroot/js/{notascredito,notascreditocompra}.js`; modificar `Web.UI/Views/Shared/_Layout.cshtml`.

---

## Fase 1 — Datos y entidades (API)

### Task 1: Script SQL y catálogo de módulos

**Files:**
- Create: `sql/2026-10-05-notas-credito.sql`
- Modify: `sql/crear-api-db-test-completo.sql`

**Interfaces:**
- Consumes: `sql/2026-10-04-notas-debito.sql` y el bloque de notas de débito de `sql/crear-api-db-test-completo.sql` (el origen de la derivación).
- Produces: tablas `NotaCredito`, `NotaCreditoDetalle`, `NotaCreditoCompra`, `NotaCreditoCompraDetalle` con las constraints de las Global Constraints; filas de numeración para `CodigoObj` `8` y `15`; módulos `NotasCredito` y `NotasCreditoCompra` en `Modulo`.

**Precondición verificable:** `sqlcmd` conecta a `localhost` con el usuario `sa` cuya clave está en `API.Service.WebApi/appsettings.json` (campo `Password=` de la cadena `API_DB`); `sql/2026-10-04-notas-debito.sql` existe; la base `API_DB_TEST` existe y **no se modifica** en este task (la verificación usa una base temporal).

- [ ] **Step 1: Derivar la migración**

Desde la raíz del repo `API` (bash):

```bash
set -e
[ -e sql/2026-10-05-notas-credito.sql ] && { echo "ya existe"; exit 1; }
sed -E \
  -e "s/'14'/'15'/g" \
  -e "s/'7'/'8'/g" \
  -e 's/ObjType 14/ObjType 15/g' -e 's/ObjType 7/ObjType 8/g' \
  -e 's/objeto 14/objeto 15/g' -e 's/objeto 7/objeto 8/g' \
  -e 's/@manualND/@manualNC/g' -e 's/@primariaND/@primariaNC/g' \
  -e 's/@manualDN/@manualNCP/g' -e 's/@primariaDN/@primariaNCP/g' \
  -e 's/Débito/Crédito/g' -e 's/débito/crédito/g' \
  -e 's/Debito/Credito/g' -e 's/debito/credito/g' \
  sql/2026-10-04-notas-debito.sql > sql/2026-10-05-notas-credito.sql
```

Revisar el resultado con estos greps (todos deben cumplirse):

```bash
grep -c "NotaCredito" sql/2026-10-05-notas-credito.sql                      # > 40
grep -n "Debito\|debito\|Débito\|débito" sql/2026-10-05-notas-credito.sql   # vacío
grep -n "TipoObjeto IN" sql/2026-10-05-notas-credito.sql                    # ('8') en cliente y detalle, ('15') en compra y detalle
grep -n "BaseTipo IN" sql/2026-10-05-notas-credito.sql                      # (-1, 6) y (-1, 13) SIN cambios
grep -n "DocAlias\|'Nota Credito" sql/2026-10-05-notas-credito.sql          # 'Nota Credito Cliente' y 'Nota Credito Prov.'
grep -n "CodigoObj = '" sql/2026-10-05-notas-credito.sql                    # '8' y '15'
grep -n "Notas de crédito" sql/2026-10-05-notas-credito.sql                 # 'Notas de crédito' y 'Notas de crédito de compra' (Ventas / Compras)
grep -n "WHEN MATCHED" sql/2026-10-05-notas-credito.sql                     # la rama de correccion del MERGE sigue presente
```

Ajustar a mano cualquier literal que no cumpla (p. ej. el comentario de cabecera: debe decir "Notas de credito de cliente (objeto 8) y de proveedor (objeto 15)"; debe conservar la línea que recomienda ejecutar con `sqlcmd -f 65001`).

- [ ] **Step 2: Actualizar el script de creación desde cero**

Desde la raíz del repo `API`, ejecutar este script node (preserva el EOL del archivo; falla ruidosamente si un ancla no existe):

```bash
node - <<'EOF'
const fs = require('fs');
const p = 'sql/crear-api-db-test-completo.sql';
let s = fs.readFileSync(p, 'utf8');
const crlf = s.includes('\r\n');
s = s.replace(/\r\n/g, '\n');
const nc = t => t
  .replace(/Débito/g, 'Crédito').replace(/débito/g, 'crédito')
  .replace(/Debito/g, 'Credito').replace(/debito/g, 'credito')
  .replace(/'14'/g, "'15'").replace(/'7'/g, "'8'")
  .replace(/ObjType 14/g, 'ObjType 15').replace(/ObjType 7/g, 'ObjType 8');
function despues(ancla, texto) {
  const i = s.indexOf(ancla);
  if (i < 0) throw new Error('ancla no encontrada: ' + ancla);
  const fin = i + ancla.length;
  s = s.slice(0, fin) + texto + s.slice(fin);
}
// 1) Tablas: clon de las 4 CREATE TABLE de notas de debito (cliente, detalle, compra, compra detalle)
const ini = s.indexOf('-- ===== NotaDebito (cliente, ObjType 7) =====');
const iniUlt = s.indexOf('CREATE TABLE dbo.NotaDebitoCompraDetalle (');
if (ini < 0 || iniUlt < 0) throw new Error('bloque de tablas de notas de debito no encontrado');
const fin = s.indexOf('\n);', iniUlt) + 3;
const bloque = s.slice(ini, fin);
s = s.slice(0, fin) + '\n\n' + nc(bloque) + s.slice(fin);
// 2) NumeracionDocumento (despues de la fila de notas de debito de proveedor)
despues("VALUES(14, 35, 'Nota Debito Prov.', '--');",
  "\nINSERT INTO NumeracionDocumento(CodigoObj, SerieDfct, DocAlias, SubTipoDoc)\nVALUES(8, 37, 'Nota Credito Cliente', '--');" +
  "\nINSERT INTO NumeracionDocumento(CodigoObj, SerieDfct, DocAlias, SubTipoDoc)\nVALUES(15, 39, 'Nota Credito Prov.', '--');");
// 3) NumeracionDocumentoDet (series 36-39)
despues("VALUES(14, 35,'Primario', 'N', '--', 'D', 1, 1);",
  "\nINSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)\nVALUES(8, 36,'Manual', 'N', '--', 'D', 'S');" +
  "\nINSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)\nVALUES(8, 37,'Primario', 'N', '--', 'D', 1, 1);" +
  "\nINSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)\nVALUES(15, 38,'Manual', 'N', '--', 'D', 'S');" +
  "\nINSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)\nVALUES(15, 39,'Primario', 'N', '--', 'D', 1, 1);");
// 4) Catalogo de modulos
despues("    ('NotasDebitoCompra', 'Notas de débito de compra', 'Compras'),",
  "\n    ('NotasCredito', 'Notas de crédito', 'Ventas'),\n    ('NotasCreditoCompra', 'Notas de crédito de compra', 'Compras'),");
if (crlf) s = s.replace(/\n/g, '\r\n');
fs.writeFileSync(p, s);
console.log('ok');
EOF
```

Notas: (a) los `INSERT` de `NumeracionDocumentoDet` quedan **antes** de las `CREATE TABLE NotaCredito*` por la posición de cada ancla (las FK de `Serie` solo necesitan que la tabla exista); (b) revisar a ojo el diff (`git diff sql/crear-api-db-test-completo.sql`) que solo hay inserciones y que las 4 tablas nuevas están entre las de notas de débito y el bloque `INV-4`.

- [ ] **Step 3: Verificar en una base temporal (NO tocar `API_DB_TEST`)**

PowerShell, desde `C:\Users\migue\source\repos\angelm0508\API`:

```powershell
$pw = ([regex]::Match((Get-Content API.Service.WebApi\appsettings.json -Raw), 'Password=([^;]*)')).Groups[1].Value
$scratch = 'C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad'
# (a) script completo en base nueva
(Get-Content sql\crear-api-db-test-completo.sql -Raw) -replace 'API_DB_TEST','API_DB_NCCHECK' | Set-Content "$scratch\ncheck.sql" -Encoding UTF8
sqlcmd -S localhost -U sa -P $pw -C -b -f 65001 -i "$scratch\ncheck.sql"
sqlcmd -S localhost -U sa -P $pw -C -d API_DB_NCCHECK -h -1 -W -f 65001 -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.tables WHERE name LIKE 'NotaCredito%'; SELECT CodigoObj, Serie, NombreSerie FROM NumeracionDocumentoDet WHERE CodigoObj IN ('8','15') ORDER BY Serie; SELECT CodigoObj, SerieDfct, DocAlias FROM NumeracionDocumento WHERE CodigoObj IN ('8','15'); SELECT Codigo, Nombre, Grupo FROM Modulo WHERE Codigo LIKE 'NotasCredito%'; SELECT COUNT(*) FROM sys.objects WHERE name IN ('pk_nota_credito','fk_nota_credito_serie','fk_nota_credito_sn','fk_nota_credito_moneda','pk_nota_credito_det','fk_nota_credito_det_cod_impuesto','pk_nota_credito_compra','fk_nota_credito_compra_serie','fk_nota_credito_compra_sn','fk_nota_credito_compra_moneda','pk_nota_credito_compra_det','fk_nota_credito_compra_det_cod_impuesto')"
# (b) migracion sobre una base sin las tablas de credito
sqlcmd -S localhost -U sa -P $pw -C -b -d API_DB_NCCHECK -Q "DROP TABLE NotaCreditoDetalle, NotaCredito, NotaCreditoCompraDetalle, NotaCreditoCompra; DELETE FROM NumeracionDocumento WHERE CodigoObj IN ('8','15'); DELETE FROM NumeracionDocumentoDet WHERE CodigoObj IN ('8','15'); DELETE FROM Modulo WHERE Codigo LIKE 'NotasCredito%'"
sqlcmd -S localhost -U sa -P $pw -C -b -f 65001 -d API_DB_NCCHECK -i sql\2026-10-05-notas-credito.sql
# (c) idempotencia
sqlcmd -S localhost -U sa -P $pw -C -b -f 65001 -d API_DB_NCCHECK -i sql\2026-10-05-notas-credito.sql
sqlcmd -S localhost -U sa -P $pw -C -d API_DB_NCCHECK -h -1 -W -f 65001 -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM NumeracionDocumentoDet WHERE CodigoObj IN ('8','15'); SELECT Nombre FROM Modulo WHERE Codigo LIKE 'NotasCredito%'"
# (d) limpieza
sqlcmd -S localhost -U sa -P $pw -C -b -Q "ALTER DATABASE API_DB_NCCHECK SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE API_DB_NCCHECK"
```

Esperado: (a) exit 0; 4 tablas; series `36,37` (CodigoObj 8) y `38,39` (CodigoObj 15); `NumeracionDocumento` con `Nota Credito Cliente`/`Nota Credito Prov.` y `SerieDfct` 37 y 39; 2 módulos con nombres con acentos correctos; las 12 constraints existen (conteo 12). (b) exit 0, series nuevas = las siguientes libres tras 35 (`36..39`). (c) segunda corrida exit 0, conteo de series 4 y nombres sin mojibake. Confirmar al final que `API_DB_NCCHECK` ya no existe y que `API_DB_TEST` no cambió (`SELECT COUNT(*) FROM sys.tables WHERE name LIKE 'NotaCredito%'` en `API_DB_TEST` = 0).

- [ ] **Step 4: Commit**

```bash
git add sql/2026-10-05-notas-credito.sql sql/crear-api-db-test-completo.sql
git commit -m "feat(db): tablas, numeracion y modulos de notas de credito (cliente y proveedor)

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Entidades EF, mapeo del contexto y repos

**Files:**
- Create: `API.Domain.Entity/Models/NotaCredito.cs`, `NotaCreditoDetalle.cs`, `NotaCreditoCompra.cs`, `NotaCreditoCompraDetalle.cs`
- Modify: `API.Domain.Entity/Models/ApiDbTestContext.cs`, `SocioNegocio.cs`, `Monedum.cs`, `NumeracionDocumentoDet.cs`, `Impuesto.cs`
- Create: `API.Infraestructure.Repository/NotaCreditoRepositorio.cs`, `NotaCreditoDetalleRepositorio.cs`, `NotaCreditoCompraRepositorio.cs`, `NotaCreditoCompraDetalleRepositorio.cs`
- Test: `API.Service.WebApi.Tests/Domain/NotaCreditoModeloTests.cs`

**Interfaces:**
- Consumes: constraints del Task 1; las entidades/repos/tests de notas de débito (origen de la derivación).
- Produces: entidades `NotaCredito`/`NotaCreditoCompra` (clave `Entry`) y `NotaCreditoDetalle`/`NotaCreditoCompraDetalle` (clave `(Entry, NoLinea)`), DbSets `NotaCreditos`, `NotaCreditoDetalles`, `NotaCreditoCompras`, `NotaCreditoCompraDetalles`; repos `NotaCreditoRepositorio : RepositorioGenericoEfCore<NotaCredito,int>`, `NotaCreditoDetalleRepositorio : RepositorioGenericoEfCore<NotaCreditoDetalle,(int Entry,int NoLinea)>` y los de compra.

**Precondición verificable:** Task 1 commiteado; `dotnet build API.sln` pasa (0 errores); `git status --porcelain` sin cambios en `API.Domain.Entity`.

- [ ] **Step 1: Test del modelo derivado (falla: las entidades no existen)**

Desde la raíz del repo `API` (bash), define `nc` (sección "La derivación `nc`") y:

```bash
nc API.Service.WebApi.Tests/Domain/NotaDebitoModeloTests.cs API.Service.WebApi.Tests/Domain/NotaCreditoModeloTests.cs
sed -i -E -e 's/"14"/"15"/g' -e 's/"7"/"8"/g' API.Service.WebApi.Tests/Domain/NotaCreditoModeloTests.cs
grep -n '"8"\|"15"\|NotaCredito' API.Service.WebApi.Tests/Domain/NotaCreditoModeloTests.cs | head -20
```

Esperado en el archivo derivado: `[InlineData(typeof(NotaCredito), "NotaCredito", "Entry", "8")]`, `[InlineData(typeof(NotaCreditoCompra), "NotaCreditoCompra", "Entry", "15")]` y los dos del detalle con `"8"`/`"15"`; ningún `Debito`. Ejecutar `dotnet test API.sln --filter "FullyQualifiedName~NotaCreditoModeloTests"` → no compila (las entidades no existen todavía).

- [ ] **Step 2: Derivar entidades y repos**

```bash
for f in NotaDebito NotaDebitoDetalle NotaDebitoCompra NotaDebitoCompraDetalle; do
  nc "API.Domain.Entity/Models/$f.cs" "API.Domain.Entity/Models/${f/NotaDebito/NotaCredito}.cs"
done
for f in NotaDebitoRepositorio NotaDebitoDetalleRepositorio NotaDebitoCompraRepositorio NotaDebitoCompraDetalleRepositorio; do
  nc "API.Infraestructure.Repository/$f.cs" "API.Infraestructure.Repository/${f/NotaDebito/NotaCredito}.cs"
done
grep -rn "Debito" API.Domain.Entity/Models/NotaCredito*.cs API.Infraestructure.Repository/NotaCredito*.cs && echo FALLO || echo OK
```

- [ ] **Step 3: Colecciones de navegación, DbSets y mapeo del contexto**

Script node (desde la raíz del repo `API`; preserva EOL/BOM; falla si un ancla no existe):

```bash
node - <<'EOF'
const fs = require('fs');
const BOM = '\uFEFF';
function leer(p) { let s = fs.readFileSync(p, 'utf8'); const crlf = s.includes('\r\n'); return { s: s.replace(/\r\n/g, '\n'), crlf }; }
function escribir(p, o) { fs.writeFileSync(p, o.crlf ? o.s.replace(/\n/g, '\r\n') : o.s); }
const nc = t => t
  .replace(/Débito/g, 'Crédito').replace(/débito/g, 'crédito')
  .replace(/Debito/g, 'Credito').replace(/debito/g, 'credito')
  .replace(/\('14'\)/g, "('15')").replace(/\('7'\)/g, "('8')");
const base = 'API.Domain.Entity/Models/';

// A) colecciones de navegacion en las entidades existentes
const colecciones = {
  'SocioNegocio.cs': ['NotaDebito', 'NotaDebitoCompra'],
  'Monedum.cs': ['NotaDebito', 'NotaDebitoCompra'],
  'NumeracionDocumentoDet.cs': ['NotaDebito', 'NotaDebitoCompra'],
  'Impuesto.cs': ['NotaDebitoDetalle', 'NotaDebitoCompraDetalle']
};
for (const [archivo, tipos] of Object.entries(colecciones)) {
  const o = leer(base + archivo);
  const lineas = tipos.map(t => {
    const re = new RegExp('^( *public virtual ICollection<' + t + '> .*)$', 'm');
    const m = o.s.match(re);
    if (!m) throw new Error(archivo + ': coleccion de ' + t + ' no encontrada');
    return nc(m[1]);
  });
  const ult = o.s.match(new RegExp('^ *public virtual ICollection<' + tipos[1] + '> .*$', 'm'));
  const fin = o.s.indexOf(ult[0]) + ult[0].length;
  o.s = o.s.slice(0, fin) + '\n\n' + lineas[0] + '\n\n' + lineas[1] + o.s.slice(fin);
  escribir(base + archivo, o);
}

// B) DbSets y mapeo en el contexto
const pc = base + 'ApiDbTestContext.cs';
const c = leer(pc);
// DbSets: bloque desde NotaDebitos hasta NotaDebitoCompraDetalles
const iDs = c.s.indexOf('    public virtual DbSet<NotaDebito> NotaDebitos { get; set; }');
const marcaDs = '    public virtual DbSet<NotaDebitoCompraDetalle> NotaDebitoCompraDetalles { get; set; }';
const iDsFin = c.s.indexOf(marcaDs);
if (iDs < 0 || iDsFin < 0) throw new Error('DbSets de notas de debito no encontrados');
const finDs = iDsFin + marcaDs.length;
c.s = c.s.slice(0, finDs) + '\n\n' + nc(c.s.slice(iDs, finDs)) + c.s.slice(finDs);
// bloques OnModelCreating: de Entity<NotaDebito> hasta el cierre del bloque de NotaDebitoCompraDetalle
const iIni = c.s.indexOf('        modelBuilder.Entity<NotaDebito>(entity =>');
const iUlt = c.s.indexOf('        modelBuilder.Entity<NotaDebitoCompraDetalle>(entity =>');
if (iIni < 0 || iUlt < 0) throw new Error('bloques de modelBuilder de notas de debito no encontrados');
const iFin = c.s.indexOf('\n        });', iUlt) + '\n        });'.length;
c.s = c.s.slice(0, iFin) + '\n\n' + nc(c.s.slice(iIni, iFin)) + c.s.slice(iFin);
escribir(pc, c);
console.log('ok');
EOF
git diff --stat
```

Verificar: `git diff` solo agrega líneas (0 eliminaciones) en los 5 archivos existentes; en `ApiDbTestContext.cs` los 4 bloques nuevos usan `.ToTable("NotaCredito")`/`...Detalle`/`...Compra`/`...CompraDetalle`, `HasDefaultValueSql("('8')")` (cliente y detalle) y `("('15')")` (compra y detalle), nombres de constraint `pk_nota_credito`, `fk_nota_credito_*`, y `WithMany(p => p.NotaCreditos)`/`NotaCreditoCompras`/`NotaCreditoDetalles`/`NotaCreditoCompraDetalles`.

- [ ] **Step 4: Ejecutar y commit**

`dotnet test API.sln` (PowerShell) → 1137 + 4 casos de `NotaCreditoModeloTests` = **1141** pasando (si el conteo difiere, reportar el real).

```bash
git add API.Domain.Entity/Models/NotaCredito.cs API.Domain.Entity/Models/NotaCreditoDetalle.cs API.Domain.Entity/Models/NotaCreditoCompra.cs API.Domain.Entity/Models/NotaCreditoCompraDetalle.cs API.Domain.Entity/Models/ApiDbTestContext.cs API.Domain.Entity/Models/SocioNegocio.cs API.Domain.Entity/Models/Monedum.cs API.Domain.Entity/Models/NumeracionDocumentoDet.cs API.Domain.Entity/Models/Impuesto.cs API.Infraestructure.Repository/NotaCreditoRepositorio.cs API.Infraestructure.Repository/NotaCreditoDetalleRepositorio.cs API.Infraestructure.Repository/NotaCreditoCompraRepositorio.cs API.Infraestructure.Repository/NotaCreditoCompraDetalleRepositorio.cs API.Service.WebApi.Tests/Domain/NotaCreditoModeloTests.cs
git commit -m "feat(notas-credito): entidades EF, mapeo y repos de nota de credito cliente y proveedor

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```


---

## Fase 2 — API: contratos, dominio, aplicación y controllers

### Task 3: DTOs, contratos de dominio/aplicación y mapeos

**Files:**
- Create (derivados): `API.Application.DTO/notaCredito/{NotaCreditoDTO,NotaCreditoCrearDTO,NotaCreditoActualizarDTO,NotaCreditoDetalleDTO,NotaCreditoDetalleCrearDTO}.cs` y `API.Application.DTO/notaCreditoCompra/{NotaCreditoCompraDTO,NotaCreditoCompraCrearDTO,NotaCreditoCompraActualizarDTO,NotaCreditoCompraDetalleDTO,NotaCreditoCompraDetalleCrearDTO}.cs`
- Create (escritos): `API.Application.DTO/notaCredito/{NotaCreditoFacturaBaseResumenDTO,NotaCreditoFacturaBaseDTO}.cs` y los equivalentes `notaCreditoCompra/NotaCreditoCompraFacturaBase{Resumen,}DTO.cs`
- Create: `API.Domain.Interface/FacturaBaseCredito.cs`, `INotaCreditoDomain.cs`, `INotaCreditoCompraDomain.cs`; `API.Application.Interface/INotaCreditoApplication.cs`, `INotaCreditoCompraApplication.cs`
- Modify: `API.Transversal.Mapper/PerfilMapeo.cs`
- Test: `API.Service.WebApi.Tests/Application/NotaCreditoMapeoTests.cs`

**Interfaces:**
- Consumes: entidades del Task 2; DTOs, interfaces y mapeos de notas de débito (origen).
- Produces (copiar exactamente; los tasks 4-9 los usan):
  - `FacturaAcreditable(Factura Documento, decimal TotalAcreditado)`, `FacturaBaseCredito(Factura Documento, IReadOnlyList<FacturaDetalle> Lineas, decimal TotalAcreditado)`, `FacturaCompraAcreditable(FacturaCompra Documento, decimal TotalAcreditado)`, `FacturaCompraBaseCredito(FacturaCompra Documento, IReadOnlyList<FacturaCompraDetalle> Lineas, decimal TotalAcreditado)`.
  - `INotaCreditoDomain`: `InsertarAsync(NotaCredito, IEnumerable<NotaCreditoDetalle>)`, `ActualizarAsync(int, NotaCredito)`, `EliminarAsync(int)`, `ObtenerAsync(int)`, `ObtenerTodoAsync()`, `ObtenerLineasAsync(int)`, `ObtenerFacturasBaseAsync(string codigoSn)` → `Task<IEnumerable<FacturaAcreditable>>`, `ObtenerFacturaBaseAsync(int entry)` → `Task<FacturaBaseCredito>`. `INotaCreditoCompraDomain` igual con `NotaCreditoCompra*`, `ObtenerFacturasCompraBaseAsync` → `Task<IEnumerable<FacturaCompraAcreditable>>`, `ObtenerFacturaCompraBaseAsync` → `Task<FacturaCompraBaseCredito>`.
  - DTOs base: `NotaCreditoFacturaBaseResumenDTO { Entry, NumDoc, FechaDoc, TotalDoc, MonedaDoc, TotalAcreditado, Disponible }` y `NotaCreditoFacturaBaseDTO { Entry, NumDoc, CodigoSn, NombreSn, MonedaDoc, Direccion, TotalDoc, TotalAcreditado, Disponible, Lineas }` (`Lineas` = `List<NotaCreditoDetalleCrearDTO>`).

**Precondición verificable:** Task 2 commiteado; `dotnet test API.sln` verde (1141).

- [ ] **Step 1: Test de mapeo derivado (falla: no existen los tipos)**

Desde la raíz del repo `API` (bash), con `nc` definida:

```bash
nc API.Service.WebApi.Tests/Application/NotaDebitoMapeoTests.cs API.Service.WebApi.Tests/Application/NotaCreditoMapeoTests.cs
grep -n "Debito\|debito" API.Service.WebApi.Tests/Application/NotaCreditoMapeoTests.cs && echo FALLO || echo OK
```

(`BaseTipo = 6`/`13` se conservan.) `dotnet test --filter "FullyQualifiedName~NotaCreditoMapeoTests"` → no compila.

- [ ] **Step 2: DTOs derivados (los 5 simples de cada documento)**

```bash
set -e
mkdir -p API.Application.DTO/notaCredito API.Application.DTO/notaCreditoCompra
for dir in notaDebito notaDebitoCompra; do
  destDir="API.Application.DTO/${dir/notaDebito/notaCredito}"
  for f in API.Application.DTO/$dir/*.cs; do
    base=$(basename "$f")
    case "$base" in *FacturaBase*) continue;; esac     # los de factura base se escriben a mano (Step 3)
    nc "$f" "$destDir/${base/NotaDebito/NotaCredito}"
  done
done
ls API.Application.DTO/notaCredito API.Application.DTO/notaCreditoCompra
grep -rn "Debito" API.Application.DTO/notaCredito API.Application.DTO/notaCreditoCompra && echo FALLO || echo OK
```

Esperado: 5 archivos por carpeta (`...DTO`, `...CrearDTO`, `...ActualizarDTO`, `...DetalleDTO`, `...DetalleCrearDTO`) y `OK`. En `NotaCreditoCompraCrearDTO.cs` el comentario de `Lineas`/`NumDoc` puede mencionar "nota de débito de proveedor" convertido a "crédito": es correcto.

- [ ] **Step 3: DTOs de factura base con saldo acreditable**

`API.Application.DTO/notaCredito/NotaCreditoFacturaBaseResumenDTO.cs`:

```csharp
namespace API.Application.DTO.notaCredito
{
    /// <summary>Resumen de una factura candidata a base de una nota de credito, con su saldo acreditable.</summary>
    public class NotaCreditoFacturaBaseResumenDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        public DateTime? FechaDoc { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? MonedaDoc { get; set; }

        /// <summary>Suma de las notas de credito no canceladas ya emitidas sobre esta factura.</summary>
        public decimal TotalAcreditado { get; set; }

        /// <summary>max(0, TotalDoc - TotalAcreditado): lo que todavia se puede acreditar.</summary>
        public decimal Disponible { get; set; }
    }
}
```

`API.Application.DTO/notaCredito/NotaCreditoFacturaBaseDTO.cs`:

```csharp
namespace API.Application.DTO.notaCredito
{
    /// <summary>Factura base con sus lineas convertidas en conceptos y su saldo acreditable.</summary>
    public class NotaCreditoFacturaBaseDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? MonedaDoc { get; set; }
        public string? Direccion { get; set; }
        public decimal? TotalDoc { get; set; }
        public decimal TotalAcreditado { get; set; }
        public decimal Disponible { get; set; }
        public List<NotaCreditoDetalleCrearDTO> Lineas { get; set; } = new();
    }
}
```

Los dos del proveedor se derivan de estos con (bash):

```bash
set -e
for f in NotaCreditoFacturaBaseResumenDTO NotaCreditoFacturaBaseDTO; do
  destino="API.Application.DTO/notaCreditoCompra/${f/NotaCredito/NotaCreditoCompra}.cs"
  [ -e "$destino" ] && { echo "ya existe $destino"; exit 1; }
  sed -E \
    -e 's/NotaCredito/NotaCreditoCompra/g' \
    -e 's/API\.Application\.DTO\.notaCredito\b/API.Application.DTO.notaCreditoCompra/g' \
    -e 's/factura candidata/factura de compra candidata/; s/Factura base/Factura de compra base/; s/esta factura/esta factura de compra/' \
    "API.Application.DTO/notaCredito/$f.cs" > "$destino"
done
grep -rn "notaCredito;" API.Application.DTO/notaCreditoCompra && echo FALLO || echo OK
```

- [ ] **Step 4: Records, interfaces de dominio y de aplicación**

`API.Domain.Interface/FacturaBaseCredito.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    /// <summary>Factura de venta candidata a base de una nota de credito de cliente, con lo ya acreditado.</summary>
    public record FacturaAcreditable(Factura Documento, decimal TotalAcreditado);

    /// <summary>Factura de venta con sus lineas y lo ya acreditado: base de una nota de credito de cliente.</summary>
    public record FacturaBaseCredito(Factura Documento, IReadOnlyList<FacturaDetalle> Lineas, decimal TotalAcreditado);

    /// <summary>Factura de compra candidata a base de una nota de credito de proveedor, con lo ya acreditado.</summary>
    public record FacturaCompraAcreditable(FacturaCompra Documento, decimal TotalAcreditado);

    /// <summary>Factura de compra con sus lineas y lo ya acreditado: base de una nota de credito de proveedor.</summary>
    public record FacturaCompraBaseCredito(FacturaCompra Documento, IReadOnlyList<FacturaCompraDetalle> Lineas, decimal TotalAcreditado);
}
```

`API.Domain.Interface/INotaCreditoDomain.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface INotaCreditoDomain
    {
        #region async methods
        Task<int> InsertarAsync(NotaCredito obj, IEnumerable<NotaCreditoDetalle> lineas);
        Task<bool> ActualizarAsync(int id, NotaCredito obj);
        Task<bool> EliminarAsync(int id);
        Task<NotaCredito> ObtenerAsync(int id);
        Task<IQueryable<NotaCredito>> ObtenerTodoAsync();
        Task<IEnumerable<NotaCreditoDetalle>> ObtenerLineasAsync(int id);
        Task<IEnumerable<FacturaAcreditable>> ObtenerFacturasBaseAsync(string codigoSn);
        Task<FacturaBaseCredito> ObtenerFacturaBaseAsync(int entry);
        #endregion
    }
}
```

`API.Domain.Interface/INotaCreditoCompraDomain.cs` (completo, no derivar):

```csharp
using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface INotaCreditoCompraDomain
    {
        #region async methods
        Task<int> InsertarAsync(NotaCreditoCompra obj, IEnumerable<NotaCreditoCompraDetalle> lineas);
        Task<bool> ActualizarAsync(int id, NotaCreditoCompra obj);
        Task<bool> EliminarAsync(int id);
        Task<NotaCreditoCompra> ObtenerAsync(int id);
        Task<IQueryable<NotaCreditoCompra>> ObtenerTodoAsync();
        Task<IEnumerable<NotaCreditoCompraDetalle>> ObtenerLineasAsync(int id);
        Task<IEnumerable<FacturaCompraAcreditable>> ObtenerFacturasCompraBaseAsync(string codigoSn);
        Task<FacturaCompraBaseCredito> ObtenerFacturaCompraBaseAsync(int entry);
        #endregion
    }
}
```

Las interfaces de aplicación son idénticas a las de débito con otro nombre (los DTOs base conservan los nombres `NotaCredito[Compra]FacturaBase[Resumen]DTO`):

```bash
nc API.Application.Interface/INotaDebitoApplication.cs API.Application.Interface/INotaCreditoApplication.cs
nc API.Application.Interface/INotaDebitoCompraApplication.cs API.Application.Interface/INotaCreditoCompraApplication.cs
grep -n "Debito\|debito" API.Application.Interface/INotaCredito*Application.cs && echo FALLO || echo OK
```

- [ ] **Step 5: Mapeos en `PerfilMapeo.cs`**

Script node (desde la raíz del repo `API`; preserva EOL; falla si un ancla falta):

```bash
node - <<'EOF'
const fs = require('fs');
const p = 'API.Transversal.Mapper/PerfilMapeo.cs';
let s = fs.readFileSync(p, 'utf8');
const crlf = s.includes('\r\n');
s = s.replace(/\r\n/g, '\n');
// usings
const uDeb = 'using API.Application.DTO.notaDebitoCompra;';
if (!s.includes(uDeb)) throw new Error('using de notaDebitoCompra no encontrado');
s = s.replace(uDeb, uDeb + '\nusing API.Application.DTO.notaCredito;\nusing API.Application.DTO.notaCreditoCompra;');
// bloque de mapeos
const ini = s.indexOf('            // NotaDebito\n');
const ancla = '            CreateMap<NotaDebitoCompraDetalleCrearDTO, NotaDebitoCompraDetalle>();';
const iFin = s.indexOf(ancla);
if (ini < 0 || iFin < 0) throw new Error('bloque de mapeos de notas de debito no encontrado');
const fin = iFin + ancla.length;
const nuevo = s.slice(ini, fin).replace(/Débito/g, 'Crédito').replace(/Debito/g, 'Credito');
s = s.slice(0, fin) + '\n\n' + nuevo + s.slice(fin);
if (crlf) s = s.replace(/\n/g, '\r\n');
fs.writeFileSync(p, s);
console.log('ok');
EOF
git diff --stat API.Transversal.Mapper/PerfilMapeo.cs
```

Esperado: solo inserciones (usings + 10 `CreateMap` de `NotaCredito*`).

- [ ] **Step 6: Ejecutar y commit**

`dotnet test API.sln` (PowerShell) verde (1141 + 4 casos del mapeo = **1145**; reportar el total real).

```bash
git add API.Application.DTO/notaCredito API.Application.DTO/notaCreditoCompra API.Domain.Interface/FacturaBaseCredito.cs API.Domain.Interface/INotaCreditoDomain.cs API.Domain.Interface/INotaCreditoCompraDomain.cs API.Application.Interface/INotaCreditoApplication.cs API.Application.Interface/INotaCreditoCompraApplication.cs API.Transversal.Mapper/PerfilMapeo.cs API.Service.WebApi.Tests/Application/NotaCreditoMapeoTests.cs
git commit -m "feat(notas-credito): DTOs, contratos de dominio/aplicacion y mapeos

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 4: `NotaCreditoDomain` (cliente) con pruebas

**Files:**
- Create: `API.Domain.Core/NotaCreditoDomain.cs`
- Test: `API.Service.WebApi.Tests/Domain/NotaCreditoDomainTests.cs`

**Interfaces:**
- Consumes: `INotaCreditoDomain`, `FacturaAcreditable`, `FacturaBaseCredito` (Task 3); entidades y repos (Task 2); `IRepositorioGenerico<T,K>`, `IEjecutorTransaccion` con `EjecutarAsync<T>(Func<Task<T>>)` y `EjecutarAsync<T>(Func<Task<T>>, System.Data.IsolationLevel)` (existentes).
- Produces: `NotaCreditoDomain : INotaCreditoDomain` con constructor `(IRepositorioGenerico<NotaCredito,int> repoNota, IRepositorioGenerico<NotaCreditoDetalle,(int Entry,int NoLinea)> repoDetalle, IRepositorioGenerico<NumeracionDocumentoDet,int> repoNumeracion, IRepositorioGenerico<SocioNegocio,string> repoSocio, IRepositorioGenerico<Impuesto,string> repoImpuesto, IRepositorioGenerico<Monedum,string> repoMoneda, IRepositorioGenerico<Factura,int> repoFactura, IRepositorioGenerico<FacturaDetalle,(int Entry,int NoLinea)> repoFacturaDetalle, IEjecutorTransaccion tx)` — en ESE orden (igual que `NotaDebitoDomain`).

**Precondición verificable:** Task 3 commiteado; `grep -n "Tasa" API.Domain.Entity/Models/Impuesto.cs` muestra `decimal? Tasa`; `grep -n "TipoSn\|Activo" API.Domain.Entity/Models/SocioNegocio.cs` muestra ambas; `grep -n "Serializable" API.Domain.Interface/IEjecutorTransaccion.cs` muestra la sobrecarga con nivel.

- [ ] **Step 1: Escribir las pruebas (fallan: el dominio no existe)**

Crear `API.Service.WebApi.Tests/Domain/NotaCreditoDomainTests.cs`:

```csharp
using System.Data;
using System.Globalization;
using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class NotaCreditoDomainTests
    {
        // ---- Constantes propias del documento (en la nota de credito de proveedor cambian estas lineas) ----
        private const string CodigoObj = "8";
        private const string CodigoObjOtro = "6";   // CodigoObj de otro documento (Factura)
        private const string TipoSocio = "C";
        private const string TipoSocioOpuesto = "P";
        private const int TipoBase = 6;
        private const string MsgSerieOtroObjeto = "La serie no corresponde a notas de crédito de cliente.";
        private const string MsgSocioTipo = "El socio de negocio debe ser un cliente.";
        private const string MsgBaseRequerida = "Indique la factura base.";
        private const string MsgBaseNoExiste = "La factura base no existe.";
        private const string MsgBaseCancelada = "La factura base está cancelada.";
        private const string MsgBaseOtroSocio = "La factura base pertenece a otro socio de negocio.";
        private const string MsgBaseMoneda = "La moneda de la nota de crédito debe ser la misma de la factura base.";
        private const string MsgExceso = "El total acreditado ({0}) excede el total de la factura base ({1}).";
        private const string MsgFacturaNoExiste = "La factura no existe.";
        private const string MsgFacturaCancelada = "La factura está cancelada.";
        // ---- fin de constantes ----

        private readonly Mock<IRepositorioGenerico<NotaCredito, int>> _repoNota = new();
        private readonly Mock<IRepositorioGenerico<NotaCreditoDetalle, (int Entry, int NoLinea)>> _repoDetalle = new();
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracion = new();
        private readonly Mock<IRepositorioGenerico<SocioNegocio, string>> _repoSocio = new();
        private readonly Mock<IRepositorioGenerico<Impuesto, string>> _repoImpuesto = new();
        private readonly Mock<IRepositorioGenerico<Monedum, string>> _repoMoneda = new();
        private readonly Mock<IRepositorioGenerico<Factura, int>> _repoFactura = new();
        private readonly Mock<IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)>> _repoFacturaDetalle = new();
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly NotaCreditoDomain _domain;

        public NotaCreditoDomainTests()
        {
            _domain = new NotaCreditoDomain(
                _repoNota.Object, _repoDetalle.Object, _repoNumeracion.Object, _repoSocio.Object,
                _repoImpuesto.Object, _repoMoneda.Object, _repoFactura.Object, _repoFacturaDetalle.Object, _tx.Object);

            // El doble del ejecutor corre el Func directo (sin transaccion ni save).
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>(), It.IsAny<IsolationLevel>()))
                .Returns<Func<Task<int>>, IsolationLevel>((f, _) => f());
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>())).Returns<Func<Task<bool>>>(f => f());
            _repoNota.Setup(r => r.InsertarAsync(It.IsAny<NotaCredito>()))
                .ReturnsAsync((NotaCredito n) => { n.Entry = 99; return n; });
            _repoNota.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<NotaCredito>().AsAsyncQueryable());
            _repoDetalle.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<NotaCreditoDetalle>())).Returns(Task.CompletedTask);

            // Valores por defecto validos: cada test rompe solo lo que quiere probar.
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie());
            _repoSocio.Setup(r => r.ObtenerAsync("SN1")).ReturnsAsync(new SocioNegocio { Codigo = "SN1", TipoSn = TipoSocio, Activo = "S" });
            _repoMoneda.Setup(r => r.ObtenerAsync("QTZ")).ReturnsAsync(new Monedum { Codigo = "QTZ" });
            _repoImpuesto.Setup(r => r.ObtenerAsync("IVA")).ReturnsAsync(new Impuesto { Codigo = "IVA", Tasa = 12m });
        }

        private static NumeracionDocumentoDet CrearSerie(int? sig = 5, int? fin = null, string bloqueado = "N", string manual = "N", string? codigoObj = null) => new()
        {
            CodigoObj = codigoObj ?? CodigoObj, Serie = 4, NombreSerie = "Primario",
            SigNumero = sig, FinNumero = fin, Bloqueado = bloqueado, Manual = manual,
            SubTipoDoc = "--", TipoSerie = "D"
        };

        private static NotaCredito Nota(Action<NotaCredito>? ajustar = null)
        {
            var n = new NotaCredito
            {
                Serie = 4, CodigoSn = "SN1", MonedaDoc = "QTZ",
                FechaDoc = new DateTime(2026, 10, 4), FechaEmision = new DateTime(2026, 10, 4)
            };
            ajustar?.Invoke(n);
            return n;
        }

        // Linea valida por defecto: 1 x 100, sin descuento, IVA 12% => total de linea 112.
        private static NotaCreditoDetalle Linea(Action<NotaCreditoDetalle>? ajustar = null)
        {
            var l = new NotaCreditoDetalle
            {
                Descripcion = "Ajuste de precio", Cantidad = 1m, Precio = 100m, PrctjeDesc = 0m, CodigoImpuesto = "IVA"
            };
            ajustar?.Invoke(l);
            return l;
        }

        private async Task<string> MensajeAlInsertar(NotaCredito nota, params NotaCreditoDetalle[] lineas)
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(nota, lineas));
            return ex.Message;
        }

        private static string Exceso(string acreditado, string total) =>
            string.Format(CultureInfo.InvariantCulture, MsgExceso, acreditado, total);

        // ---------------- InsertarAsync: camino feliz ----------------

        [Fact]
        public async Task InsertarAsync_Valida_NumeraGuardaEncabezadoYLineas()
        {
            var serie = CrearSerie(sig: 5);
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            var nota = Nota();

            var entry = await _domain.InsertarAsync(nota, new[] { Linea(), Linea(l => l.Descripcion = "Descuento comercial") });

            Assert.Equal(99, entry);
            Assert.Equal(CodigoObj, nota.TipoObjeto);
            Assert.Equal(5, nota.NumDoc);
            Assert.Equal(6, serie.SigNumero);          // arrancaba en 5: avanzo
            Assert.Equal("N", nota.NumManual);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>(), IsolationLevel.Serializable), Times.Once);
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(It.Is<NotaCreditoDetalle>(l => l.Entry == 99 && l.NoLinea == 1 && l.TipoObjeto == CodigoObj)), Times.Once);
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(It.Is<NotaCreditoDetalle>(l => l.Entry == 99 && l.NoLinea == 2 && l.Descripcion == "Descuento comercial")), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_FuerzaLosCamposDeSistema()
        {
            var nota = Nota(n =>
            {
                n.Cancelado = "S"; n.EstadoDoc = "C"; n.Imprimido = "S"; n.FechaCancelado = DateTime.Now; n.TipoObjeto = "6";
            });

            await _domain.InsertarAsync(nota, new[] { Linea() });

            Assert.Equal("N", nota.Cancelado);
            Assert.Equal("A", nota.EstadoDoc);
            Assert.Equal("N", nota.Imprimido);
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

        // ---------------- InsertarAsync: totales calculados por el servidor ----------------

        [Fact]
        public async Task InsertarAsync_RecalculaLineasYTotales_IgnorandoLoQueMandaElCliente()
        {
            // L1: 3 x 10.10 = 30.30; desc 10% = 3.03; subtotal 27.27; IVA 12% = 3.2724 -> 3.27; total 30.54
            // L2: 2 x 5 = 10.00; sin descuento; IVA 12% = 1.20; total 11.20
            var l1 = Linea(l => { l.Cantidad = 3m; l.Precio = 10.10m; l.PrctjeDesc = 10m; l.Impuesto = 999m; l.TotalLinea = 999m; l.PrecioBruto = 999m; });
            var l2 = Linea(l => { l.Cantidad = 2m; l.Precio = 5m; l.Impuesto = 888m; l.TotalLinea = 888m; });
            var nota = Nota(n => { n.TotalBruto = 1m; n.TotalDesc = 1m; n.TotalImp = 1m; n.TotalDoc = 1m; });

            await _domain.InsertarAsync(nota, new[] { l1, l2 });

            Assert.Equal(30.30m, l1.PrecioBruto);
            Assert.Equal(3.27m, l1.Impuesto);
            Assert.Equal(30.54m, l1.TotalLinea);
            Assert.Equal(10.00m, l2.PrecioBruto);
            Assert.Equal(1.20m, l2.Impuesto);
            Assert.Equal(11.20m, l2.TotalLinea);
            Assert.Equal(40.30m, nota.TotalBruto);
            Assert.Equal(3.03m, nota.TotalDesc);
            Assert.Equal(4.47m, nota.TotalImp);
            Assert.Equal(41.74m, nota.TotalDoc);
        }

        [Fact]
        public async Task InsertarAsync_Redondea_ALaAlzaEnElPuntoMedio()
        {
            var linea = Linea(l => { l.Cantidad = 1m; l.Precio = 0.005m; });
            var nota = Nota();

            await _domain.InsertarAsync(nota, new[] { linea });

            Assert.Equal(0.01m, linea.PrecioBruto);   // 0.005 -> 0.01 (AwayFromZero)
            Assert.Equal(0.00m, linea.Impuesto);      // 0.01 x 12% = 0.0012 -> 0.00
            Assert.Equal(0.01m, linea.TotalLinea);
        }

        [Fact]
        public async Task InsertarAsync_ImpuestoExentoOSinTasa_NoSumaImpuesto()
        {
            _repoImpuesto.Setup(r => r.ObtenerAsync("EXE")).ReturnsAsync(new Impuesto { Codigo = "EXE", Tasa = 0m });
            _repoImpuesto.Setup(r => r.ObtenerAsync("SINTASA")).ReturnsAsync(new Impuesto { Codigo = "SINTASA", Tasa = null });
            var l1 = Linea(l => l.CodigoImpuesto = "EXE");
            var l2 = Linea(l => l.CodigoImpuesto = "SINTASA");
            var nota = Nota();

            await _domain.InsertarAsync(nota, new[] { l1, l2 });

            Assert.Equal(0m, l1.Impuesto);
            Assert.Equal(100m, l1.TotalLinea);
            Assert.Equal(0m, l2.Impuesto);
            Assert.Equal(200m, nota.TotalDoc);
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

            Assert.Equal("La serie está bloqueada y no se puede usar para registrar notas de crédito.", await MensajeAlInsertar(Nota(), Linea()));
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
            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(Nota(), Array.Empty<NotaCreditoDetalle>()));

            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>(), It.IsAny<IsolationLevel>()), Times.Never);
            _repoNota.Verify(r => r.InsertarAsync(It.IsAny<NotaCredito>()), Times.Never);
        }

        // ---------------- InsertarAsync: factura base ----------------

        private void PrepararFactura(int entry = 12, string socio = "SN1", string cancelado = "N", string moneda = "QTZ", decimal total = 1000m) =>
            _repoFactura.Setup(r => r.ObtenerAsync(entry)).ReturnsAsync(new Factura
            {
                Entry = entry, CodigoSn = socio, Cancelado = cancelado, MonedaDoc = moneda, TotalDoc = total
            });

        private static NotaCredito NotaConBase(int entry = 12) => Nota(n => { n.BaseTipo = TipoBase; n.BaseEntry = entry; });

        private static NotaCredito Previa(int baseEntry, decimal total, string cancelado = "N", int baseTipo = TipoBase) =>
            new() { BaseTipo = baseTipo, BaseEntry = baseEntry, TotalDoc = total, Cancelado = cancelado };

        private void NotasPrevias(params NotaCredito[] notas) =>
            _repoNota.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(notas.ToList().AsAsyncQueryable());

        [Fact]
        public async Task InsertarAsync_ConBaseValida_ConservaLaReferencia()
        {
            PrepararFactura();
            var nota = NotaConBase();

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
            Assert.Equal(MsgBaseNoExiste, await MensajeAlInsertar(NotaConBase(777), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_ConBaseCancelada_Lanza()
        {
            PrepararFactura(cancelado: "S");

            Assert.Equal(MsgBaseCancelada, await MensajeAlInsertar(NotaConBase(), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_ConBaseDeOtroSocio_Lanza()
        {
            PrepararFactura(socio: "OTRO");

            Assert.Equal(MsgBaseOtroSocio, await MensajeAlInsertar(NotaConBase(), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_ConBaseDeOtraMoneda_Lanza()
        {
            PrepararFactura(moneda: "USD");

            Assert.Equal(MsgBaseMoneda, await MensajeAlInsertar(NotaConBase(), Linea()));
            _repoNota.Verify(r => r.InsertarAsync(It.IsAny<NotaCredito>()), Times.Never);
        }

        // ---------------- InsertarAsync: limite acreditable ----------------

        [Fact]
        public async Task InsertarAsync_TotalIgualAlDeLaFactura_EsValido()
        {
            PrepararFactura(total: 112m);          // la nota (1 x 100 + IVA 12%) suma exactamente 112

            var entry = await _domain.InsertarAsync(NotaConBase(), new[] { Linea() });

            Assert.Equal(99, entry);
        }

        [Fact]
        public async Task InsertarAsync_TotalMayorAlDeLaFactura_LanzaYNoGuarda()
        {
            PrepararFactura(total: 100m);

            Assert.Equal(Exceso("112.00", "100.00"), await MensajeAlInsertar(NotaConBase(), Linea()));
            _repoNota.Verify(r => r.InsertarAsync(It.IsAny<NotaCredito>()), Times.Never);
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(It.IsAny<NotaCreditoDetalle>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_ConNotasPreviasQueSumanMasDelTotal_Lanza()
        {
            PrepararFactura(total: 200m);
            NotasPrevias(Previa(12, 50m), Previa(12, 40m));   // 90 ya acreditado; la nueva es 112 => 202 > 200

            Assert.Equal(Exceso("202.00", "200.00"), await MensajeAlInsertar(NotaConBase(), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_ConNotasPreviasDentroDelLimite_EsValido()
        {
            PrepararFactura(total: 200m);
            NotasPrevias(Previa(12, 50m));                    // 50 + 112 = 162 <= 200

            var entry = await _domain.InsertarAsync(NotaConBase(), new[] { Linea() });

            Assert.Equal(99, entry);
        }

        [Fact]
        public async Task InsertarAsync_IgnoraLasNotasPreviasCanceladas_DeOtraFacturaOSinBase()
        {
            PrepararFactura(total: 120m);
            NotasPrevias(
                Previa(12, 100m, cancelado: "S"),             // cancelada: libera su monto
                Previa(99, 500m),                             // de otra factura
                Previa(12, 300m, baseTipo: -1));              // sin base de factura de venta

            var entry = await _domain.InsertarAsync(NotaConBase(), new[] { Linea() });   // 112 <= 120

            Assert.Equal(99, entry);
        }

        [Fact]
        public async Task InsertarAsync_ComparaALosDosDecimales()
        {
            PrepararFactura(total: 112.004m);                 // se redondea a 112.00

            var entry = await _domain.InsertarAsync(NotaConBase(), new[] { Linea() });

            Assert.Equal(99, entry);
        }

        [Fact]
        public async Task InsertarAsync_SinBase_NoTieneLimiteNiConsultaLasNotasPrevias()
        {
            var linea = Linea(l => l.Precio = 1_000_000m);

            var entry = await _domain.InsertarAsync(Nota(), new[] { linea });

            Assert.Equal(99, entry);
            _repoNota.Verify(r => r.ObtenerTodoAsync(), Times.Never);
            _repoFactura.Verify(r => r.ObtenerAsync(It.IsAny<int>()), Times.Never);
        }

        // ---------------- ActualizarAsync ----------------

        [Fact]
        public async Task ActualizarAsync_Inexistente_DevuelveFalse()
        {
            _repoNota.Setup(r => r.ObtenerAsync(5)).ReturnsAsync((NotaCredito?)null);

            Assert.False(await _domain.ActualizarAsync(5, new NotaCredito()));
        }

        [Fact]
        public async Task ActualizarAsync_Cancelar_MarcaCanceladoYFechaSinTocarElComentario()
        {
            var existente = new NotaCredito { Entry = 5, Cancelado = "N", Comentario = "original" };
            _repoNota.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(existente);

            var ok = await _domain.ActualizarAsync(5, new NotaCredito { Cancelado = "S" });

            Assert.True(ok);
            Assert.Equal("S", existente.Cancelado);
            Assert.NotNull(existente.FechaCancelado);
            Assert.Equal("original", existente.Comentario);
            Assert.Equal("A", existente.EstadoDoc ?? "A");   // EstadoDoc no cambia al cancelar
        }

        [Fact]
        public async Task ActualizarAsync_CancelarConComentario_ActualizaElComentario()
        {
            var existente = new NotaCredito { Entry = 5, Cancelado = "N", Comentario = "original" };
            _repoNota.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(existente);

            await _domain.ActualizarAsync(5, new NotaCredito { Cancelado = "S", Comentario = "motivo" });

            Assert.Equal("motivo", existente.Comentario);
        }

        [Fact]
        public async Task ActualizarAsync_SoloComentario_NoCancelaNiCambiaOtrosCampos()
        {
            var existente = new NotaCredito { Entry = 5, Cancelado = "N", Comentario = "viejo", TotalDoc = 50m, CodigoSn = "SN1" };
            _repoNota.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(existente);

            await _domain.ActualizarAsync(5, new NotaCredito { Cancelado = "N", Comentario = "nuevo", TotalDoc = 999m, CodigoSn = "OTRO" });

            Assert.Equal("nuevo", existente.Comentario);
            Assert.Equal("N", existente.Cancelado);
            Assert.Equal(50m, existente.TotalDoc);
            Assert.Equal("SN1", existente.CodigoSn);
        }

        [Fact]
        public async Task ActualizarAsync_DocumentoCancelado_NoSePuedeModificar()
        {
            _repoNota.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(new NotaCredito { Entry = 5, Cancelado = "S" });

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(5, new NotaCredito { Comentario = "x" }));

            Assert.Equal("El documento está cancelado y no se puede modificar.", ex.Message);
        }

        // ---------------- EliminarAsync ----------------

        [Fact]
        public async Task EliminarAsync_Inexistente_DevuelveFalse()
        {
            _repoNota.Setup(r => r.ObtenerAsync(5)).ReturnsAsync((NotaCredito?)null);

            Assert.False(await _domain.EliminarAsync(5));
        }

        [Fact]
        public async Task EliminarAsync_SinCancelar_Lanza()
        {
            _repoNota.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(new NotaCredito { Entry = 5, Cancelado = "N" });

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(5));

            Assert.Equal("Cancele el documento (Cancelado='S') antes de eliminarlo.", ex.Message);
            _repoNota.Verify(r => r.EliminarAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task EliminarAsync_Cancelado_BorraLasLineasYElEncabezado()
        {
            _repoNota.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(new NotaCredito { Entry = 5, Cancelado = "S" });
            var lineas = new List<NotaCreditoDetalle>
            {
                new() { Entry = 5, NoLinea = 1 }, new() { Entry = 5, NoLinea = 2 }, new() { Entry = 6, NoLinea = 1 }
            };
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(lineas.AsAsyncQueryable());
            _repoDetalle.Setup(r => r.EliminarAsync(It.IsAny<(int Entry, int NoLinea)>())).ReturnsAsync(true);
            _repoNota.Setup(r => r.EliminarAsync(5)).ReturnsAsync(true);

            var ok = await _domain.EliminarAsync(5);

            Assert.True(ok);
            _repoDetalle.Verify(r => r.EliminarAsync(It.Is<(int Entry, int NoLinea)>(k => k.Entry == 5 && k.NoLinea == 1)), Times.Once);
            _repoDetalle.Verify(r => r.EliminarAsync(It.Is<(int Entry, int NoLinea)>(k => k.Entry == 5 && k.NoLinea == 2)), Times.Once);
            _repoDetalle.Verify(r => r.EliminarAsync(It.Is<(int Entry, int NoLinea)>(k => k.Entry == 6 && k.NoLinea == 1)), Times.Never);
            _repoNota.Verify(r => r.EliminarAsync(5), Times.Once);
        }

        // ---------------- Consultas ----------------

        [Fact]
        public async Task ObtenerLineasAsync_DevuelveSoloLasDelDocumentoEnOrden()
        {
            var lineas = new List<NotaCreditoDetalle>
            {
                new() { Entry = 5, NoLinea = 2 }, new() { Entry = 6, NoLinea = 1 }, new() { Entry = 5, NoLinea = 1 }
            };
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(lineas.AsAsyncQueryable());

            var resultado = (await _domain.ObtenerLineasAsync(5)).ToList();

            Assert.Equal(new[] { 1, 2 }, resultado.Select(l => l.NoLinea));
        }

        [Fact]
        public async Task ObtenerFacturasBaseAsync_DevuelveLasNoCanceladasDelSocioConLoYaAcreditado()
        {
            var facturas = new List<Factura>
            {
                new() { Entry = 1, CodigoSn = "SN1", Cancelado = "N", TotalDoc = 112m },
                new() { Entry = 2, CodigoSn = "SN1", Cancelado = "S", TotalDoc = 50m },
                new() { Entry = 3, CodigoSn = "OTRO", Cancelado = "N", TotalDoc = 70m },
                new() { Entry = 4, CodigoSn = "SN1", Cancelado = "N", TotalDoc = 200m }
            };
            _repoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(facturas.AsAsyncQueryable());
            NotasPrevias(Previa(1, 30m), Previa(1, 20m, cancelado: "S"), Previa(3, 10m));

            var resultado = (await _domain.ObtenerFacturasBaseAsync("SN1")).ToList();

            Assert.Equal(new[] { 4, 1 }, resultado.Select(f => f.Documento.Entry));   // de la mas nueva a la mas vieja
            Assert.Equal(0m, resultado[0].TotalAcreditado);
            Assert.Equal(30m, resultado[1].TotalAcreditado);                           // la cancelada no cuenta
        }

        [Fact]
        public async Task ObtenerFacturaBaseAsync_DevuelveEncabezadoLineasOrdenadasYAcreditado()
        {
            PrepararFactura(entry: 12, total: 112m);
            var lineas = new List<FacturaDetalle>
            {
                new() { Entry = 12, NoLinea = 2 }, new() { Entry = 13, NoLinea = 1 }, new() { Entry = 12, NoLinea = 1 }
            };
            _repoFacturaDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(lineas.AsAsyncQueryable());
            NotasPrevias(Previa(12, 25m), Previa(12, 10m, cancelado: "S"));

            var resultado = await _domain.ObtenerFacturaBaseAsync(12);

            Assert.Equal(12, resultado.Documento.Entry);
            Assert.Equal(new[] { 1, 2 }, resultado.Lineas.Select(l => l.NoLinea));
            Assert.Equal(25m, resultado.TotalAcreditado);
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

- [ ] **Step 2: Ver el fallo** — `dotnet test API.sln --filter "FullyQualifiedName~NotaCreditoDomainTests"` no compila (`NotaCreditoDomain` no existe).

- [ ] **Step 3: Implementar `API.Domain.Core/NotaCreditoDomain.cs`**

```csharp
using System.Data;
using System.Globalization;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class NotaCreditoDomain : INotaCreditoDomain
    {
        // ---- Constantes propias del documento (en la nota de credito de proveedor cambian estas lineas) ----
        // CHECK constraint de la tabla: TipoObjeto='8'. Se fuerza siempre en el servidor.
        private const string TipoObjetoNotaCredito = "8";
        private const string TipoSocio = "C";
        private const int TipoDocBase = 6;
        private const string MsgSerieOtroObjeto = "La serie no corresponde a notas de crédito de cliente.";
        private const string MsgSocioTipo = "El socio de negocio debe ser un cliente.";
        private const string MsgBaseRequerida = "Indique la factura base.";
        private const string MsgBaseNoExiste = "La factura base no existe.";
        private const string MsgBaseCancelada = "La factura base está cancelada.";
        private const string MsgBaseOtroSocio = "La factura base pertenece a otro socio de negocio.";
        private const string MsgBaseMoneda = "La moneda de la nota de crédito debe ser la misma de la factura base.";
        private const string MsgExceso = "El total acreditado ({0}) excede el total de la factura base ({1}).";
        private const string MsgFacturaNoExiste = "La factura no existe.";
        private const string MsgFacturaCancelada = "La factura está cancelada.";
        // ---- fin de constantes ----

        private const int SinBase = -1;
        private const int LongitudMaximaDescripcion = 200;

        private readonly IRepositorioGenerico<NotaCredito, int> _repoNota;
        private readonly IRepositorioGenerico<NotaCreditoDetalle, (int Entry, int NoLinea)> _repoDetalle;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoNumeracion;
        private readonly IRepositorioGenerico<SocioNegocio, string> _repoSocio;
        private readonly IRepositorioGenerico<Impuesto, string> _repoImpuesto;
        private readonly IRepositorioGenerico<Monedum, string> _repoMoneda;
        private readonly IRepositorioGenerico<Factura, int> _repoFactura;
        private readonly IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> _repoFacturaDetalle;
        private readonly IEjecutorTransaccion _tx;

        public NotaCreditoDomain(
            IRepositorioGenerico<NotaCredito, int> repoNota,
            IRepositorioGenerico<NotaCreditoDetalle, (int Entry, int NoLinea)> repoDetalle,
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
        public async Task<int> InsertarAsync(NotaCredito obj, IEnumerable<NotaCreditoDetalle> lineas)
        {
            var lineasList = lineas?.ToList() ?? new List<NotaCreditoDetalle>();

            obj.TipoObjeto = TipoObjetoNotaCredito;
            obj.Cancelado = "N";                  // un documento recien registrado nunca nace cancelado
            obj.EstadoDoc = "A";
            obj.Imprimido = "N";
            obj.FechaCancelado = null;
            obj.FechaDoc ??= DateTime.Now;
            obj.FechaEmision ??= obj.FechaDoc;

            var serie = await ValidarSerieAsync(obj);
            await ValidarSocioYMonedaAsync(obj);
            var tasas = await ValidarLineasAsync(lineasList);
            RecalcularTotales(obj, lineasList, tasas);
            var facturaBase = await ValidarBaseAsync(obj);

            AsignarNumero(obj, serie);

            // Serializable: el chequeo del limite acreditable y el insert son una unidad; dos notas
            // simultaneas sobre la misma factura no pueden exceder el total entre las dos.
            return await _tx.EjecutarAsync(async () =>
            {
                if (facturaBase is not null)
                    await ValidarLimiteAcreditableAsync(facturaBase, obj);

                await _repoNota.InsertarAsync(obj); // Save #1: asigna obj.Entry

                var noLinea = 1;
                foreach (var linea in lineasList)
                {
                    linea.Entry = obj.Entry;
                    linea.NoLinea = noLinea++;
                    linea.TipoObjeto = TipoObjetoNotaCredito;
                    await _repoDetalle.AgregarSinGuardarAsync(linea);
                }

                return obj.Entry;
            }, IsolationLevel.Serializable);
            // EjecutarAsync: Save #2 (lineas) + Commit. No hay asiento de inventario: la nota es solo monetaria.
        }

        public async Task<bool> ActualizarAsync(int id, NotaCredito obj)
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

        public async Task<NotaCredito> ObtenerAsync(int id)
        {
            return await _repoNota.ObtenerAsync(id);
        }

        public async Task<IQueryable<NotaCredito>> ObtenerTodoAsync()
        {
            return await _repoNota.ObtenerTodoAsync();
        }

        public async Task<IEnumerable<NotaCreditoDetalle>> ObtenerLineasAsync(int id)
        {
            var queryable = await _repoDetalle.ObtenerTodoAsync();
            return await queryable.Where(d => d.Entry == id).OrderBy(d => d.NoLinea).ToListAsync();
        }

        public async Task<IEnumerable<FacturaAcreditable>> ObtenerFacturasBaseAsync(string codigoSn)
        {
            var queryable = await _repoFactura.ObtenerTodoAsync();
            var facturas = await queryable
                .Where(f => f.CodigoSn == codigoSn && f.Cancelado != "S")
                .OrderByDescending(f => f.Entry)
                .ToListAsync();

            if (facturas.Count == 0)
                return new List<FacturaAcreditable>();

            // Una sola consulta de las notas no canceladas sobre esas facturas; se suma en memoria.
            var entries = facturas.Select(f => f.Entry).ToList();
            var notas = await (await _repoNota.ObtenerTodoAsync())
                .Where(n => n.BaseTipo == TipoDocBase && n.Cancelado != "S" && n.BaseEntry != null && entries.Contains(n.BaseEntry.Value))
                .Select(n => new { Base = n.BaseEntry, Total = n.TotalDoc ?? 0m })
                .ToListAsync();
            var acreditadoPorFactura = notas
                .GroupBy(n => n.Base!.Value)
                .ToDictionary(g => g.Key, g => Redondear(g.Sum(n => n.Total)));

            return facturas
                .Select(f => new FacturaAcreditable(f, acreditadoPorFactura.TryGetValue(f.Entry, out var a) ? a : 0m))
                .ToList();
        }

        public async Task<FacturaBaseCredito> ObtenerFacturaBaseAsync(int entry)
        {
            var factura = await _repoFactura.ObtenerAsync(entry)
                ?? throw new Exception(MsgFacturaNoExiste);

            if (factura.Cancelado == "S")
                throw new Exception(MsgFacturaCancelada);

            var queryable = await _repoFacturaDetalle.ObtenerTodoAsync();
            var lineas = await queryable.Where(l => l.Entry == entry).OrderBy(l => l.NoLinea).ToListAsync();
            var acreditado = await ObtenerAcreditadoAsync(entry);
            return new FacturaBaseCredito(factura, lineas, acreditado);
        }
        #endregion

        #region validaciones y calculos
        private static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

        private async Task<NumeracionDocumentoDet> ValidarSerieAsync(NotaCredito obj)
        {
            var serie = await _repoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            if (serie.CodigoObj != TipoObjetoNotaCredito)
                throw new Exception(MsgSerieOtroObjeto);

            if (serie.Bloqueado == "S")
                throw new Exception("La serie está bloqueada y no se puede usar para registrar notas de crédito.");

            if (serie.Manual == "S" && obj.NumDoc <= 0)
                throw new Exception("El número de documento es requerido para series manuales.");

            return serie;
        }

        // La numeracion automatica se asigna al final (despues de validar todo) para no avanzar el
        // consecutivo de una serie tracked si el documento resulta invalido.
        private static void AsignarNumero(NotaCredito obj, NumeracionDocumentoDet serie)
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

        private async Task ValidarSocioYMonedaAsync(NotaCredito obj)
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

        /// <summary>Valida las lineas y devuelve la tasa (%) de cada codigo de impuesto usado.</summary>
        private async Task<Dictionary<string, decimal>> ValidarLineasAsync(IList<NotaCreditoDetalle> lineas)
        {
            if (lineas.Count == 0)
                throw new Exception("El documento debe tener al menos una línea.");

            var tasas = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

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

                if (!tasas.ContainsKey(linea.CodigoImpuesto))
                {
                    var impuesto = await _repoImpuesto.ObtenerAsync(linea.CodigoImpuesto)
                        ?? throw new Exception($"Línea {n}: el impuesto '{linea.CodigoImpuesto}' no existe.");
                    tasas[linea.CodigoImpuesto] = impuesto.Tasa ?? 0m;
                }
            }

            return tasas;
        }

        /// <summary>
        /// El servidor es la unica fuente de los montos: recalcula impuesto y total de cada linea y los
        /// totales del encabezado; lo que mande el cliente se ignora.
        /// </summary>
        private static void RecalcularTotales(NotaCredito obj, IList<NotaCreditoDetalle> lineas, IReadOnlyDictionary<string, decimal> tasas)
        {
            decimal totalBruto = 0m, totalDesc = 0m, totalImp = 0m, totalDoc = 0m;

            foreach (var linea in lineas)
            {
                var bruto = Redondear(linea.Cantidad!.Value * linea.Precio!.Value);
                var descuento = Redondear(bruto * linea.PrctjeDesc!.Value / 100m);
                var subtotal = bruto - descuento;
                var impuesto = Redondear(subtotal * tasas[linea.CodigoImpuesto] / 100m);

                linea.PrecioBruto = bruto;
                linea.Impuesto = impuesto;
                linea.TotalLinea = subtotal + impuesto;

                totalBruto += bruto;
                totalDesc += descuento;
                totalImp += impuesto;
                totalDoc += linea.TotalLinea.Value;
            }

            obj.TotalBruto = totalBruto;
            obj.TotalDesc = totalDesc;
            obj.TotalImp = totalImp;
            obj.TotalDoc = totalDoc;
        }

        /// <summary>Valida la factura base (si hay) y la devuelve; sin base devuelve null.</summary>
        private async Task<Factura?> ValidarBaseAsync(NotaCredito obj)
        {
            var tipo = obj.BaseTipo ?? SinBase;

            if (tipo == SinBase)
            {
                obj.BaseTipo = SinBase;
                obj.BaseEntry = null;
                return null;
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

            if (!string.Equals(factura.MonedaDoc, obj.MonedaDoc, StringComparison.OrdinalIgnoreCase))
                throw new Exception(MsgBaseMoneda);

            return factura;
        }

        private async Task<decimal> ObtenerAcreditadoAsync(int facturaEntry)
        {
            var queryable = await _repoNota.ObtenerTodoAsync();
            var totales = await queryable
                .Where(n => n.BaseTipo == TipoDocBase && n.BaseEntry == facturaEntry && n.Cancelado != "S")
                .Select(n => n.TotalDoc ?? 0m)
                .ToListAsync();
            return Redondear(totales.Sum());
        }

        /// <summary>Corre dentro de la transaccion Serializable del insert.</summary>
        private async Task ValidarLimiteAcreditableAsync(Factura factura, NotaCredito obj)
        {
            var acreditado = await ObtenerAcreditadoAsync(factura.Entry);
            var totalConEstaNota = Redondear(acreditado + (obj.TotalDoc ?? 0m));
            var totalFactura = Redondear(factura.TotalDoc ?? 0m);

            if (totalConEstaNota > totalFactura)
                throw new Exception(string.Format(CultureInfo.InvariantCulture, MsgExceso,
                    totalConEstaNota.ToString("0.00", CultureInfo.InvariantCulture),
                    totalFactura.ToString("0.00", CultureInfo.InvariantCulture)));
        }
        #endregion
    }
}
```

- [ ] **Step 4: Ejecutar las pruebas del dominio**

`dotnet test API.sln --filter "FullyQualifiedName~NotaCreditoDomainTests"` → todas verdes. Si un detalle de Moq/nullable impide compilar una prueba (p. ej. `ReturnsAsync((NumeracionDocumentoDet?)null)`), ajustar el test manteniendo su aserción; **no** cambiar mensajes del dominio ni la lógica para que pase un test (si un cálculo esperado del test parece incorrecto, verificarlo a mano contra las fórmulas de las Global Constraints y reportarlo).

- [ ] **Step 5: Suite completa y commit**

`dotnet test API.sln` verde (reportar el total real).

```bash
git add API.Domain.Core/NotaCreditoDomain.cs API.Service.WebApi.Tests/Domain/NotaCreditoDomainTests.cs
git commit -m "feat(notas-credito): dominio de nota de credito de cliente con totales del servidor y limite acreditable

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 5: `NotaCreditoCompraDomain` (proveedor) derivado del cliente

**Files:**
- Create: `API.Domain.Core/NotaCreditoCompraDomain.cs`
- Test: `API.Service.WebApi.Tests/Domain/NotaCreditoCompraDomainTests.cs`

**Interfaces:**
- Consumes: `NotaCreditoDomain` y `NotaCreditoDomainTests` (Task 4); `INotaCreditoCompraDomain`, `FacturaCompraAcreditable`, `FacturaCompraBaseCredito` (Task 3).
- Produces: `NotaCreditoCompraDomain : INotaCreditoCompraDomain` con el mismo orden de constructor, usando `NotaCreditoCompra`, `NotaCreditoCompraDetalle`, `FacturaCompra`, `FacturaCompraDetalle`; métodos `ObtenerFacturasCompraBaseAsync(string)` y `ObtenerFacturaCompraBaseAsync(int)`.

**Precondición verificable:** Task 4 commiteado y verde; `grep -n "Descripcion\|CodigoImpuesto\|TotalLinea\|CodigoSn\|Cancelado\|MonedaDoc\|TotalDoc" API.Domain.Entity/Models/FacturaCompra.cs API.Domain.Entity/Models/FacturaCompraDetalle.cs` muestra esas propiedades.

- [ ] **Step 1: Generar dominio y pruebas del proveedor**

Desde la raíz del repo `API` (bash). El orden de los `sed` importa: valores propios del documento primero, luego renombres estructurales.

```bash
set -e
mirror() {   # $1 = origen (cliente)  $2 = destino (proveedor, NUEVO)
  [ -e "$2" ] && { echo "ya existe $2"; return 1; }
  sed -E \
    -e 's|private const string TipoObjetoNotaCredito = "8";|private const string TipoObjetoNotaCredito = "15";|' \
    -e 's|private const string TipoSocio = "C";|private const string TipoSocio = "P";|' \
    -e 's|private const int TipoDocBase = 6;|private const int TipoDocBase = 13;|' \
    -e 's|private const string CodigoObj = "8";|private const string CodigoObj = "15";|' \
    -e 's|private const string CodigoObjOtro = "6";|private const string CodigoObjOtro = "13";|' \
    -e 's|private const string TipoSocioOpuesto = "P";|private const string TipoSocioOpuesto = "C";|' \
    -e 's|private const int TipoBase = 6;|private const int TipoBase = 13;|' \
    -e 's|notas de crédito de cliente\.|notas de crédito de proveedor.|' \
    -e 's|debe ser un cliente\.|debe ser un proveedor.|' \
    -e 's|"Indique la factura base\."|"Indique la factura de compra base."|' \
    -e 's|"La factura base no existe\."|"La factura de compra base no existe."|' \
    -e 's|"La factura base está cancelada\."|"La factura de compra base está cancelada."|' \
    -e 's|"La factura base pertenece a otro socio de negocio\."|"La factura de compra base pertenece a otro socio de negocio."|' \
    -e 's|misma de la factura base\.|misma de la factura de compra base.|' \
    -e 's|total de la factura base \(|total de la factura de compra base (|' \
    -e 's|"La factura no existe\."|"La factura de compra no existe."|' \
    -e 's|"La factura está cancelada\."|"La factura de compra está cancelada."|' \
    -e 's/ObtenerFacturasBaseAsync/ObtenerFacturasCompraBaseAsync/g' \
    -e 's/ObtenerFacturaBaseAsync/ObtenerFacturaCompraBaseAsync/g' \
    -e 's/NotaCredito/NotaCreditoCompra/g' \
    -e 's/FacturaAcreditable/FacturaCompraAcreditable/g' \
    -e 's/FacturaBaseCredito/FacturaCompraBaseCredito/g' \
    -e 's/FacturaDetalle/FacturaCompraDetalle/g' \
    -e 's/_repoFactura\b/_repoFacturaCompra/g' \
    -e 's/\brepoFactura\b/repoFacturaCompra/g' \
    -e 's/\bFactura\b/FacturaCompra/g' \
    "$1" > "$2"
}
mirror API.Domain.Core/NotaCreditoDomain.cs API.Domain.Core/NotaCreditoCompraDomain.cs
mirror API.Service.WebApi.Tests/Domain/NotaCreditoDomainTests.cs API.Service.WebApi.Tests/Domain/NotaCreditoCompraDomainTests.cs
```

- [ ] **Step 2: Revisar lo generado**

```bash
diff API.Domain.Core/NotaCreditoDomain.cs API.Domain.Core/NotaCreditoCompraDomain.cs | grep '^>' | head -120
```

Deben aparecer solo: nombres con `Compra`, las constantes `"15"`/`"P"`/`13` y los mensajes de proveedor, `ObtenerFacturasCompraBaseAsync/ObtenerFacturaCompraBaseAsync`, `_repoFacturaCompra`, `FacturaCompraAcreditable`, `FacturaCompraBaseCredito`, `FacturaCompraDetalle`. Verificar a mano (el `sed` no puede garantizarlo): (a) la propiedad del record sigue siendo `Documento` y el código usa `.Documento`; (b) el comentario de `TipoObjetoNotaCreditoCompra` dice `TipoObjeto='15'` (editar el texto del comentario si quedó "8"); (c) en la clase de pruebas: `grep -n '"8"\|= 6;\|"6"' API.Service.WebApi.Tests/Domain/NotaCreditoCompraDomainTests.cs` solo puede devolver líneas que son entradas deliberadamente "de otro objeto" (p. ej. `n.TipoObjeto = "6"` en el test de campos del sistema); (d) en las pruebas de proveedor `Previa(...)`, `BaseTipo` usan `TipoBase` (13) y la moneda por defecto sigue `QTZ`; (e) el mensaje de exceso esperado en las pruebas usa la constante `MsgExceso` con "factura de compra base".

- [ ] **Step 3: Pruebas, suite completa y commit**

`dotnet test API.sln --filter "FullyQualifiedName~NotaCreditoCompraDomainTests"` → verde (un fallo indica un literal que el `sed` no cubrió: corregirlo en el archivo derivado, no en el del cliente). Luego `dotnet test API.sln` completo verde (reportar total).

```bash
git add API.Domain.Core/NotaCreditoCompraDomain.cs API.Service.WebApi.Tests/Domain/NotaCreditoCompraDomainTests.cs
git commit -m "feat(notas-credito): dominio de nota de credito de proveedor con totales del servidor y limite acreditable

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```


---

### Task 6: Aplicación, controllers y DI (cliente y proveedor) con pruebas

**Files:**
- Create: `API.Application.Main/NotaCreditoApplication.cs`, `NotaCreditoCompraApplication.cs`
- Create (derivados): `API.Service.WebApi/Controllers/NotaCreditoController.cs`, `NotaCreditoCompraController.cs`
- Modify: `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Application/NotaCreditoApplicationTests.cs`, `NotaCreditoCompraApplicationTests.cs`; `API.Service.WebApi.Tests/Controllers/NotaCreditoControllerTests.cs`, `NotaCreditoCompraControllerTests.cs`, `NotaCreditoPermisosTests.cs`

**Interfaces:**
- Consumes: `INotaCreditoDomain`/`INotaCreditoCompraDomain`, records `FacturaAcreditable`/`FacturaBaseCredito` y los de compra, DTOs e `INotaCreditoApplication`/`INotaCreditoCompraApplication` (Task 3); dominios (Tasks 4-5); `PermisoUsuario.Tiene`/`Denegado` y `[RequierePermiso]` (existentes); controllers/tests de notas de débito (origen).
- Produces: rutas REST con `[Authorize]`: `GET api/NotaCredito/{id}` · `GET api/NotaCredito` · `GET api/NotaCredito/{id}/Lineas` (`NotasCredito`/`Ver`); `GET api/NotaCredito/FacturasBase/{codigoSn}` y `GET api/NotaCredito/FacturaBase/{entry}` (`Agregar`); `POST api/NotaCredito` (`Agregar`); `PUT api/NotaCredito/{id}` (`Actualizar`, y `Eliminar` si `Cancelado == "S"`); `DELETE api/NotaCredito/{id}` (`Eliminar`). Proveedor: `api/NotaCreditoCompra`, módulo `NotasCreditoCompra`, rutas de base `FacturasCompraBase/{codigoSn}` y `FacturaCompraBase/{entry}`. Los DTOs de base incluyen `TotalAcreditado` y `Disponible` (= `max(0, TotalDoc − TotalAcreditado)` a 2 decimales).

**Precondición verificable:** Tasks 4 y 5 commiteados; `dotnet test API.sln` verde.

- [ ] **Step 1: Pruebas de aplicación derivadas (fallan: no existe `NotaCreditoApplication`)**

Desde la raíz del repo `API` (bash), con `nc` definida (sección "La derivación `nc`"):

```bash
nc API.Service.WebApi.Tests/Application/NotaDebitoApplicationTests.cs API.Service.WebApi.Tests/Application/NotaCreditoApplicationTests.cs
node - <<'EOF'
const fs = require('fs');
const p = 'API.Service.WebApi.Tests/Application/NotaCreditoApplicationTests.cs';
let s = fs.readFileSync(p, 'utf8');
const crlf = s.includes('\r\n');
s = s.replace(/\r\n/g, '\n');
function reemplazarMetodo(nombre, nuevo) {
  const i = s.indexOf('public async Task ' + nombre + '()');
  if (i < 0) throw new Error('metodo no encontrado: ' + nombre);
  const ini = s.lastIndexOf('        [Fact]', i);
  const fin = s.indexOf('\n        }\n', i) + '\n        }\n'.length;
  s = s.slice(0, ini) + nuevo + s.slice(fin);
}
reemplazarMetodo('ObtenerFacturasBase_MapeaElResumen', `        [Fact]
        public async Task ObtenerFacturasBase_MapeaElResumenConAcreditadoYDisponible()
        {
            _domain.Setup(d => d.ObtenerFacturasBaseAsync("SN1")).ReturnsAsync(new List<FacturaAcreditable>
            {
                new(new Factura { Entry = 12, NumDoc = 100, FechaDoc = new DateTime(2026, 10, 1), TotalDoc = 112m, MonedaDoc = "QTZ", CodigoSn = "SN1" }, 30m),
                new(new Factura { Entry = 13, NumDoc = 101, TotalDoc = 50m, MonedaDoc = "QTZ", CodigoSn = "SN1" }, 80m)   // acreditado > total: disponible 0
            });

            var respuesta = await _app.ObtenerFacturasBaseAsync("SN1");

            var lista = respuesta.Dato.ToList();
            Assert.Equal(2, lista.Count);
            Assert.Equal(12, lista[0].Entry);
            Assert.Equal(100, lista[0].NumDoc);
            Assert.Equal(112m, lista[0].TotalDoc);
            Assert.Equal("QTZ", lista[0].MonedaDoc);
            Assert.Equal(30m, lista[0].TotalAcreditado);
            Assert.Equal(82m, lista[0].Disponible);
            Assert.Equal(80m, lista[1].TotalAcreditado);
            Assert.Equal(0m, lista[1].Disponible);
        }
`);
reemplazarMetodo('ObtenerFacturaBase_ConvierteLasLineasEnConceptos', `        [Fact]
        public async Task ObtenerFacturaBase_ConvierteLasLineasEnConceptos_YDevuelveElDisponible()
        {
            var factura = new Factura { Entry = 12, NumDoc = 100, CodigoSn = "SN1", NombreSn = "Cliente", MonedaDoc = "QTZ", Direccion = "Zona 1", TotalDoc = 112m };
            var lineas = new List<FacturaDetalle>
            {
                new() { Entry = 12, NoLinea = 1, CodArticulo = "ART1", Descripcion = "Tornillo", Cantidad = 3m, Precio = 5m, PrecioBruto = 15m, PrctjeDesc = 0m, CodigoImpuesto = "IVA", Impuesto = 1.8m, TotalLinea = 16.8m },
                new() { Entry = 12, NoLinea = 2, CodArticulo = "ART2", Descripcion = "  ", Cantidad = 1m, Precio = 10m, CodigoImpuesto = "IVA" }
            };
            _domain.Setup(d => d.ObtenerFacturaBaseAsync(12)).ReturnsAsync(new FacturaBaseCredito(factura, lineas, 12m));

            var respuesta = await _app.ObtenerFacturaBaseAsync(12);

            Assert.True(respuesta.Resultado);
            var dato = respuesta.Dato;
            Assert.Equal((12, 100, "SN1", "Cliente", "QTZ", "Zona 1"), (dato.Entry, dato.NumDoc, dato.CodigoSn, dato.NombreSn, dato.MonedaDoc, dato.Direccion));
            Assert.Equal(112m, dato.TotalDoc);
            Assert.Equal(12m, dato.TotalAcreditado);
            Assert.Equal(100m, dato.Disponible);
            Assert.Equal(2, dato.Lineas.Count);
            Assert.Equal("Tornillo", dato.Lineas[0].Descripcion);
            Assert.Equal(16.8m, dato.Lineas[0].TotalLinea);
            Assert.Equal("ART2", dato.Lineas[1].Descripcion);   // sin descripcion: usa el codigo del articulo
        }
`);
if (crlf) s = s.replace(/\n/g, '\r\n');
fs.writeFileSync(p, s);
console.log('ok');
EOF
grep -n "Debito\|debito" API.Service.WebApi.Tests/Application/NotaCreditoApplicationTests.cs && echo FALLO || echo OK
```

`dotnet test --filter "FullyQualifiedName~NotaCreditoApplicationTests"` → no compila (falta `NotaCreditoApplication`).

- [ ] **Step 2: Implementar `API.Application.Main/NotaCreditoApplication.cs`**

```csharp
using API.Application.DTO;
using API.Application.DTO.notaCredito;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class NotaCreditoApplication : INotaCreditoApplication
    {
        private readonly INotaCreditoDomain _domain;
        private readonly IMapper _mapper;

        public NotaCreditoApplication(INotaCreditoDomain domain, IMapper mapper)
        {
            _domain = domain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(NotaCreditoCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var nota = _mapper.Map<NotaCredito>(obj);
                var lineas = _mapper.Map<IEnumerable<NotaCreditoDetalle>>(obj.Lineas);
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

        public async Task<Respuesta<bool>> ActualizarAsync(int id, NotaCreditoActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var nota = _mapper.Map<NotaCredito>(obj);
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

        public async Task<Respuesta<NotaCreditoDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<NotaCreditoDTO>();
            try
            {
                var nota = await _domain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<NotaCreditoDTO>(nota);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<NotaCreditoDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<NotaCreditoDTO>>();
            try
            {
                var queryable = await _domain.ObtenerTodoAsync();
                var notas = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<NotaCreditoDTO>>(notas);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<NotaCreditoDetalleDTO>>> ObtenerLineasAsync(int id)
        {
            var respuesta = new Respuesta<IEnumerable<NotaCreditoDetalleDTO>>();
            try
            {
                var lineas = await _domain.ObtenerLineasAsync(id);
                respuesta.Dato = _mapper.Map<IEnumerable<NotaCreditoDetalleDTO>>(lineas);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<NotaCreditoFacturaBaseResumenDTO>>> ObtenerFacturasBaseAsync(string codigoSn)
        {
            var respuesta = new Respuesta<IEnumerable<NotaCreditoFacturaBaseResumenDTO>>();
            try
            {
                var facturas = await _domain.ObtenerFacturasBaseAsync(codigoSn);
                respuesta.Dato = facturas.Select(f => new NotaCreditoFacturaBaseResumenDTO
                {
                    Entry = f.Documento.Entry,
                    NumDoc = f.Documento.NumDoc,
                    FechaDoc = f.Documento.FechaDoc,
                    TotalDoc = f.Documento.TotalDoc,
                    MonedaDoc = f.Documento.MonedaDoc,
                    TotalAcreditado = f.TotalAcreditado,
                    Disponible = Disponible(f.Documento.TotalDoc, f.TotalAcreditado)
                }).ToList();
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<NotaCreditoFacturaBaseDTO>> ObtenerFacturaBaseAsync(int entry)
        {
            var respuesta = new Respuesta<NotaCreditoFacturaBaseDTO>();
            try
            {
                var baseDoc = await _domain.ObtenerFacturaBaseAsync(entry);
                respuesta.Dato = new NotaCreditoFacturaBaseDTO
                {
                    Entry = baseDoc.Documento.Entry,
                    NumDoc = baseDoc.Documento.NumDoc,
                    CodigoSn = baseDoc.Documento.CodigoSn,
                    NombreSn = baseDoc.Documento.NombreSn,
                    MonedaDoc = baseDoc.Documento.MonedaDoc,
                    Direccion = baseDoc.Documento.Direccion,
                    TotalDoc = baseDoc.Documento.TotalDoc,
                    TotalAcreditado = baseDoc.TotalAcreditado,
                    Disponible = Disponible(baseDoc.Documento.TotalDoc, baseDoc.TotalAcreditado),
                    // Las lineas de la factura se convierten en conceptos: sin articulo ni almacen.
                    Lineas = baseDoc.Lineas.Select(l => new NotaCreditoDetalleCrearDTO
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

        /// <summary>Lo que todavia se puede acreditar de una factura: max(0, total - acreditado), a 2 decimales.</summary>
        private static decimal Disponible(decimal? totalFactura, decimal acreditado) =>
            Math.Max(0m, Math.Round((totalFactura ?? 0m) - acreditado, 2, MidpointRounding.AwayFromZero));
    }
}
```

- [ ] **Step 3: Controllers y sus pruebas (derivados de los de débito)**

```bash
nc API.Service.WebApi/Controllers/NotaDebitoController.cs API.Service.WebApi/Controllers/NotaCreditoController.cs
nc API.Service.WebApi/Controllers/NotaDebitoCompraController.cs API.Service.WebApi/Controllers/NotaCreditoCompraController.cs
nc API.Service.WebApi.Tests/Controllers/NotaDebitoControllerTests.cs API.Service.WebApi.Tests/Controllers/NotaCreditoControllerTests.cs
nc API.Service.WebApi.Tests/Controllers/NotaDebitoCompraControllerTests.cs API.Service.WebApi.Tests/Controllers/NotaCreditoCompraControllerTests.cs
nc API.Service.WebApi.Tests/Controllers/NotaDebitoPermisosTests.cs API.Service.WebApi.Tests/Controllers/NotaCreditoPermisosTests.cs
grep -rn "Debito\|debito" API.Service.WebApi/Controllers/NotaCredito*.cs API.Service.WebApi.Tests/Controllers/NotaCredito*.cs && echo FALLO || echo OK
grep -n "Route(\|HttpGet(\|RequierePermiso" API.Service.WebApi/Controllers/NotaCreditoCompraController.cs | head -20
```

Esperado: `OK`; en el controller de proveedor `[Route("api/NotaCreditoCompra")]`, `[HttpGet("FacturasCompraBase/{codigoSn}")]`, `[HttpGet("FacturaCompraBase/{entry:int}")]` y módulo `"NotasCreditoCompra"` en todos los `[RequierePermiso]` y en `PermisoUsuario.Tiene`; los dos de base con `Accion.Agregar`. Los mensajes 404 dicen "nota de crédito" (los tests derivados esperan el mismo texto).

- [ ] **Step 4: Registrar en `Startup.cs`**

Desde la raíz del repo `API` (bash; preserva EOL):

```bash
node - <<'EOF'
const fs = require('fs');
const p = 'API.Service.WebApi/Startup.cs';
let s = fs.readFileSync(p, 'utf8');
const crlf = s.includes('\r\n');
s = s.replace(/\r\n/g, '\n');
const ancla = '            services.AddTransient<INotaDebitoCompraApplication, NotaDebitoCompraApplication>();';
const i = s.indexOf(ancla);
if (i < 0) throw new Error('registro de notas de debito no encontrado');
const fin = i + ancla.length;
const bloque = `

            services.AddTransient<IRepositorioGenerico<NotaCredito, int>, NotaCreditoRepositorio>();
            services.AddTransient<IRepositorioGenerico<NotaCreditoDetalle, (int Entry, int NoLinea)>, NotaCreditoDetalleRepositorio>();
            services.AddTransient<INotaCreditoDomain, NotaCreditoDomain>();
            services.AddTransient<INotaCreditoApplication, NotaCreditoApplication>();

            services.AddTransient<IRepositorioGenerico<NotaCreditoCompra, int>, NotaCreditoCompraRepositorio>();
            services.AddTransient<IRepositorioGenerico<NotaCreditoCompraDetalle, (int Entry, int NoLinea)>, NotaCreditoCompraDetalleRepositorio>();
            services.AddTransient<INotaCreditoCompraDomain, NotaCreditoCompraDomain>();
            services.AddTransient<INotaCreditoCompraApplication, NotaCreditoCompraApplication>();`;
s = s.slice(0, fin) + bloque + s.slice(fin);
if (crlf) s = s.replace(/\n/g, '\r\n');
fs.writeFileSync(p, s);
console.log('ok');
EOF
git diff --stat API.Service.WebApi/Startup.cs
```

Esperado: solo inserciones (+10). Confirmar con `grep` que cada dependencia del constructor de `NotaCreditoDomain`/`NotaCreditoCompraDomain` está registrada en `Startup.cs` (`NumeracionDocumentoDet`, `SocioNegocio`, `Impuesto`, `Monedum`, `Factura`, `FacturaDetalle`, `FacturaCompra`, `FacturaCompraDetalle`, `IEjecutorTransaccion`); reportar cualquiera que falte.

- [ ] **Step 5: Derivar aplicación y pruebas del proveedor**

Desde la raíz del repo `API` (bash). Atención: `NotaCreditoFacturaBase*DTO` contienen "FacturaBase", por eso aquí no hay una regla suelta `FacturaBase`; los records se renombran con reglas explícitas.

```bash
set -e
mirror2() {   # $1 = origen (cliente)  $2 = destino (proveedor, NUEVO)
  [ -e "$2" ] && { echo "ya existe $2"; return 1; }
  sed -E \
    -e 's/ObtenerFacturasBase/ObtenerFacturasCompraBase/g' \
    -e 's/ObtenerFacturaBase/ObtenerFacturaCompraBase/g' \
    -e 's/NotaCredito/NotaCreditoCompra/g' \
    -e 's/FacturaAcreditable/FacturaCompraAcreditable/g' \
    -e 's/FacturaBaseCredito/FacturaCompraBaseCredito/g' \
    -e 's/FacturaDetalle/FacturaCompraDetalle/g' \
    -e 's/\bFactura\b/FacturaCompra/g' \
    -e 's/API\.Application\.DTO\.notaCredito\b/API.Application.DTO.notaCreditoCompra/g' \
    -e 's/La factura no existe\./La factura de compra no existe./g' \
    "$1" > "$2"
}
mirror2 API.Application.Main/NotaCreditoApplication.cs API.Application.Main/NotaCreditoCompraApplication.cs
mirror2 API.Service.WebApi.Tests/Application/NotaCreditoApplicationTests.cs API.Service.WebApi.Tests/Application/NotaCreditoCompraApplicationTests.cs
grep -nE "NotaCredito([^C]|$)|notaCredito;" API.Application.Main/NotaCreditoCompraApplication.cs API.Service.WebApi.Tests/Application/NotaCreditoCompraApplicationTests.cs && echo FALLO || echo OK
```

Revisar a mano en `NotaCreditoCompraApplication.cs`: `using API.Application.DTO.notaCreditoCompra;`; los dos métodos de base usan `baseDoc.Documento`/`.Lineas`/`.TotalAcreditado` y `f.Documento`; el método estático `Disponible` se conserva; en el archivo de pruebas, `new(new FacturaCompra { ... }, 30m)`, `List<FacturaCompraAcreditable>`, `new FacturaCompraBaseCredito(factura, lineas, 12m)`, `List<FacturaCompraDetalle>` y el mensaje `"La factura de compra no existe."`.

- [ ] **Step 6: Ejecutar y commit**

`dotnet test API.sln` (PowerShell) → todo verde (reportar el total real; si una prueba derivada falla por un literal que el `sed` no cubrió, corregirlo en el archivo derivado).

```bash
git add API.Application.Main/NotaCreditoApplication.cs API.Application.Main/NotaCreditoCompraApplication.cs API.Service.WebApi/Controllers/NotaCreditoController.cs API.Service.WebApi/Controllers/NotaCreditoCompraController.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Application/NotaCreditoApplicationTests.cs API.Service.WebApi.Tests/Application/NotaCreditoCompraApplicationTests.cs API.Service.WebApi.Tests/Controllers/NotaCreditoControllerTests.cs API.Service.WebApi.Tests/Controllers/NotaCreditoCompraControllerTests.cs API.Service.WebApi.Tests/Controllers/NotaCreditoPermisosTests.cs
git commit -m "feat(notas-credito): aplicacion, controllers y registro de DI de nota de credito cliente y proveedor

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Fase 3 — Web

### Task 7: Clientes de API y DTOs de la Web

**Files:**
- Create: `Web.ApiClient/Dtos/NotaCredito/*.cs` (7), `Web.ApiClient/Dtos/NotaCreditoCompra/*.cs` (7)
- Create: `Web.ApiClient/Clientes/INotaCreditoApiClient.cs`, `NotaCreditoApiClient.cs`, `INotaCreditoCompraApiClient.cs`, `NotaCreditoCompraApiClient.cs`
- Modify: `Web.UI/Program.cs`

**Interfaces:**
- Consumes: DTOs de la API (Task 3, `..\API\API.Application.DTO\notaCredito*`) y rutas REST del Task 6; clientes de notas de débito (origen).
- Produces: `INotaCreditoApiClient` con `ObtenerTodoAsync()`, `ObtenerAsync(int entry)`, `InsertarAsync(NotaCreditoCrearDTO)`, `ActualizarAsync(int entry, NotaCreditoActualizarDTO)`, `EliminarAsync(int entry)`, `ObtenerLineasAsync(int entry)`, `ObtenerFacturasBaseAsync(string codigoSn)`, `ObtenerFacturaBaseAsync(int entry)`; y `INotaCreditoCompraApiClient` equivalente con `ObtenerFacturasCompraBaseAsync`/`ObtenerFacturaCompraBaseAsync`. DTOs en `Web.ApiClient.Dtos.NotaCredito` y `Web.ApiClient.Dtos.NotaCreditoCompra` (idénticos a los de la API salvo el namespace).

**Precondición verificable:** en el repo Web, `git status --porcelain` lista únicamente los 4 archivos ajenos; `dotnet build Web.slnx` pasa; el repo API tiene los DTOs `notaCredito`/`notaCreditoCompra` commiteados (Task 3).

- [ ] **Step 1: Generar los DTOs de la Web desde los de la API**

Desde la raíz del repo `Web` (bash):

```bash
set -e
api=../API/API.Application.DTO
for par in "notaCredito:NotaCredito" "notaCreditoCompra:NotaCreditoCompra"; do
  carpetaApi="${par%%:*}"; nombre="${par##*:}"
  destino="Web.ApiClient/Dtos/$nombre"
  [ -e "$destino" ] && { echo "ya existe $destino"; exit 1; }
  mkdir -p "$destino"
  for f in "$api/$carpetaApi"/*.cs; do
    sed "s/namespace API\.Application\.DTO\.$carpetaApi/namespace Web.ApiClient.Dtos.$nombre/" "$f" > "$destino/$(basename "$f")"
  done
done
grep -rn "API.Application" Web.ApiClient/Dtos/NotaCredito Web.ApiClient/Dtos/NotaCreditoCompra && echo FALLO || echo OK
ls Web.ApiClient/Dtos/NotaCredito Web.ApiClient/Dtos/NotaCreditoCompra
grep -n "Disponible\|TotalAcreditado" Web.ApiClient/Dtos/NotaCredito/NotaCreditoFacturaBase*.cs Web.ApiClient/Dtos/NotaCreditoCompra/NotaCreditoCompraFacturaBase*.cs
```

Esperado: `OK`, 7 archivos por carpeta y las propiedades `TotalAcreditado`/`Disponible` en los 4 DTOs de factura base.

- [ ] **Step 2: Clientes (derivados de los de débito)**

```bash
set -e
nc() {   # (misma definicion que la seccion "La derivacion nc")
  [ -e "$2" ] && { echo "El destino ya existe: $2"; return 1; }
  sed -E -e 's/Débito/Crédito/g' -e 's/débito/crédito/g' -e 's/Debito/Credito/g' -e 's/debito/credito/g' "$1" > "$2"
}
for f in INotaDebitoApiClient NotaDebitoApiClient INotaDebitoCompraApiClient NotaDebitoCompraApiClient; do
  nc "Web.ApiClient/Clientes/$f.cs" "Web.ApiClient/Clientes/${f/NotaDebito/NotaCredito}.cs"
done
grep -rn "Debito" Web.ApiClient/Clientes/*NotaCredito*.cs && echo FALLO || echo OK
grep -n "Recurso =\|FacturasBase\|FacturaBase\|FacturasCompraBase\|FacturaCompraBase" Web.ApiClient/Clientes/NotaCreditoApiClient.cs Web.ApiClient/Clientes/NotaCreditoCompraApiClient.cs
```

Esperado: `api/NotaCredito` / `api/NotaCreditoCompra` y las rutas de base `/FacturasBase/`, `/FacturaBase/` (cliente) y `/FacturasCompraBase/`, `/FacturaCompraBase/` (proveedor), idénticas a los controllers de la API.

- [ ] **Step 3: Registro en `Program.cs`**

```bash
node - <<'EOF'
const fs = require('fs');
const p = 'Web.UI/Program.cs';
let s = fs.readFileSync(p, 'utf8');
const crlf = s.includes('\r\n');
s = s.replace(/\r\n/g, '\n');
const ancla = 'builder.Services.AddHttpClient<INotaDebitoCompraApiClient, NotaDebitoCompraApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))\n    .AddHttpMessageHandler<JwtAuthorizationHandler>();';
const i = s.indexOf(ancla);
if (i < 0) throw new Error('registro de INotaDebitoCompraApiClient no encontrado');
const fin = i + ancla.length;
const bloque = '\nbuilder.Services.AddHttpClient<INotaCreditoApiClient, NotaCreditoApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))\n    .AddHttpMessageHandler<JwtAuthorizationHandler>();\nbuilder.Services.AddHttpClient<INotaCreditoCompraApiClient, NotaCreditoCompraApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))\n    .AddHttpMessageHandler<JwtAuthorizationHandler>();';
s = s.slice(0, fin) + bloque + s.slice(fin);
if (crlf) s = s.replace(/\n/g, '\r\n');
fs.writeFileSync(p, s);
console.log('ok');
EOF
git diff --stat Web.UI/Program.cs
```

- [ ] **Step 4: Build y commit**

`dotnet build Web.slnx` con el `BaseOutputPath` de la cabecera → 0 errores (las 4 advertencias de los archivos ajenos son esperadas).

```bash
git add Web.ApiClient/Dtos/NotaCredito Web.ApiClient/Dtos/NotaCreditoCompra Web.ApiClient/Clientes/INotaCreditoApiClient.cs Web.ApiClient/Clientes/NotaCreditoApiClient.cs Web.ApiClient/Clientes/INotaCreditoCompraApiClient.cs Web.ApiClient/Clientes/NotaCreditoCompraApiClient.cs Web.UI/Program.cs
git commit -m "feat(notas-credito): clientes de API y DTOs de nota de credito en la Web

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Pantalla Web de Notas de crédito de cliente

**Files:**
- Create: `Web.UI/Controllers/NotasCreditoController.cs`, `Web.UI/Views/NotasCredito/Index.cshtml`, `Web.UI/Views/NotasCredito/_Form.cshtml`, `Web.UI/wwwroot/js/notascredito.js`

**Interfaces:**
- Consumes: `INotaCreditoApiClient` y DTOs (Task 7); los 4 archivos equivalentes de notas de débito (origen); `App.*` de `wwwroot/js/site.js`.
- Produces: rutas MVC `NotasCredito/{Index, ObtenerTodos, FormularioCrear, FormularioEditar, Crear, Editar, Eliminar, ObtenerDetalle, BuscarSocios, BuscarImpuestos, ObtenerImpuestoPorCodigo, BuscarFacturasBase, ObtenerFacturaBase}` con los mismos permisos que las de débito (módulo `NotasCredito`). Constantes propias del documento en el controller y el JS (el proveedor las cambia en el Task 9).

**Precondición verificable:** Task 7 commiteado; `git status --porcelain` lista solo los 4 archivos ajenos.

- [ ] **Step 1: Derivar los 4 archivos**

Desde la raíz del repo `Web` (bash), con `nc` definida:

```bash
set -e
mkdir -p Web.UI/Views/NotasCredito
nc Web.UI/Controllers/NotasDebitoController.cs Web.UI/Controllers/NotasCreditoController.cs
nc Web.UI/Views/NotasDebito/Index.cshtml Web.UI/Views/NotasCredito/Index.cshtml
nc Web.UI/Views/NotasDebito/_Form.cshtml Web.UI/Views/NotasCredito/_Form.cshtml
nc Web.UI/wwwroot/js/notasdebito.js Web.UI/wwwroot/js/notascredito.js
sed -i -E 's/(CodigoObjNotaCredito = )"7"/\1"8"/' Web.UI/Controllers/NotasCreditoController.cs
grep -rn "Debito\|debito\|Débito\|débito" Web.UI/Controllers/NotasCreditoController.cs Web.UI/Views/NotasCredito Web.UI/wwwroot/js/notascredito.js && echo FALLO || echo OK
grep -n 'CodigoObjNotaCredito =\|TipoSocio =' Web.UI/Controllers/NotasCreditoController.cs     # "8" y "C"
grep -n 'TIPO_BASE =' Web.UI/wwwroot/js/notascredito.js                                         # 6 (sin cambio)
```

- [ ] **Step 2: Parches de la vista (aviso de exceso)**

En `Web.UI/Views/NotasCredito/_Form.cshtml`, justo después del `<span class="form-text" id="textoFacturaBase">…</span>` agregar el aviso. Script (falla si el ancla no existe):

```bash
node - <<'EOF'
const fs = require('fs');
const p = 'Web.UI/Views/NotasCredito/_Form.cshtml';
let s = fs.readFileSync(p, 'utf8');
const crlf = s.includes('\r\n');
s = s.replace(/\r\n/g, '\n');
const m = s.match(/^( *)<span class="form-text" id="textoFacturaBase">.*<\/span>$/m);
if (!m) throw new Error('span textoFacturaBase no encontrado');
const aviso = m[1] + '<div id="alertaExcedeBase" class="alert alert-warning py-2 px-3 small mt-2 d-none"></div>';
s = s.replace(m[0], m[0] + '\n' + aviso);
if (crlf) s = s.replace(/\n/g, '\r\n');
fs.writeFileSync(p, s);
console.log('ok');
EOF
grep -n "alertaExcedeBase" Web.UI/Views/NotasCredito/_Form.cshtml
```

- [ ] **Step 3: Parches del JS (saldo acreditable y aviso)**

Guardar este script como `C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\parche-nc-js.js` (se reutiliza en el Task 9) y ejecutarlo con el archivo JS como argumento. Falla si algún ancla falta:

```javascript
// Uso: node parche-nc-js.js <ruta-al-js>
const fs = require('fs');
const p = process.argv[2];
let s = fs.readFileSync(p, 'utf8');
const crlf = s.includes('\r\n');
s = s.replace(/\r\n/g, '\n');

function una(re, desc) {
  const m = s.match(re);
  if (!m) throw new Error('ancla no encontrada: ' + desc);
  return m;
}

// URL de BuscarFacturasBase de este archivo (cliente o proveedor)
const urlBuscar = una(/'(\/NotasCredito(?:Compra)?\/BuscarFacturasBase)'/, 'url BuscarFacturasBase')[1];

// (a) estado y funcion de aviso, antes de hayFacturaBase()
const ancla = '    function hayFacturaBase() {';
if (!s.includes(ancla)) throw new Error('hayFacturaBase no encontrada');
s = s.replace(ancla, () => `    // Saldo acreditable de la factura base cargada (null = sin base); el servidor es quien rechaza si se excede.
    let baseDisponible = null;

    function actualizarAlertaExceso() {
        const $a = $('#alertaExcedeBase');
        if ($a.length === 0) return;
        const total = Number($('#TotalDoc').val()) || 0;
        const excede = baseDisponible !== null && total > baseDisponible + 0.005;
        $a.toggleClass('d-none', !excede);
        if (excede) {
            $a.text(\`El total (\${total.toFixed(2)}) excede lo disponible para acreditar de la factura base (\${baseDisponible.toFixed(2)}). El servidor rechazará la nota.\`);
        }
    }

` + ancla);

// (b) reemplazar cargarFacturasBase completa (cuerpo hasta la llave de 4 espacios)
const ini = s.indexOf('    async function cargarFacturasBase() {');
if (ini < 0) throw new Error('cargarFacturasBase no encontrada');
const fin = s.indexOf('\n    }\n', ini) + '\n    }'.length;
const nuevaFuncion = `    async function cargarFacturasBase() {
        const $sel = $('#selectFacturaBase');
        if ($sel.length === 0) return;

        baseDisponible = null;
        actualizarAlertaExceso();
        const codigoSn = $('#CodigoSn').val();
        $('#btnCargarFacturaBase').prop('disabled', true);
        if (!codigoSn) {
            $sel.html('<option value="">-- Selecciona un socio para ver sus facturas --</option>').prop('disabled', true);
            return;
        }

        let lista = [];
        try {
            const respuesta = await $.get('${urlBuscar}', { codigoSn });
            if ($('#CodigoSn').val() !== codigoSn) return;   // respuesta atrasada de un socio anterior
            lista = (respuesta.resultado && respuesta.dato) ? respuesta.dato : [];
        } catch (e) {
            if ($('#CodigoSn').val() !== codigoSn) return;
        }

        $sel.html('<option value="">-- Sin factura base --</option>').prop('disabled', false);
        // Solo se ofrecen las facturas que todavia tienen saldo acreditable.
        lista.filter(f => Number(f.disponible ?? 0) > 0).forEach(f => {
            const fecha = f.fechaDoc ? new Date(f.fechaDoc).toLocaleDateString() : '';
            const total = f.totalDoc != null ? Number(f.totalDoc).toFixed(2) : '';
            const disponible = Number(f.disponible).toFixed(2);
            $sel.append($('<option>').val(f.entry).text(\`#\${f.numDoc} - \${fecha} - \${f.monedaDoc ?? ''} \${total} (disponible: \${disponible})\`));
        });
    }`;
s = s.slice(0, ini) + nuevaFuncion + s.slice(fin);

// (c) al cargar la factura base: fijar el disponible y mostrarlo en el texto
const reTexto = /\$\('#textoFacturaBase'\)\.text\(`(Basada en la factura(?: de compra)? #)\$\{factura\.numDoc\}\.[^`]*`\);/;
una(reTexto, 'texto Basada en la factura');
s = s.replace(reTexto, (_m, prefijo) =>
  "baseDisponible = Number(factura.disponible ?? 0);\n        $('#textoFacturaBase').text(`" + prefijo +
  "${factura.numDoc}. Disponible para acreditar: ${baseDisponible.toFixed(2)}. Puedes ajustar las líneas antes de guardar.`);");

// (d) quitar la base: limpiar el disponible
una(/\$\('#baseEntry'\)\.val\(''\);/, "$('#baseEntry').val('')");
s = s.replace("$('#baseEntry').val('');", () => "$('#baseEntry').val('');\n        baseDisponible = null;\n        actualizarAlertaExceso();");

// (e) pintarDetalle: refrescar el aviso despues de actualizar los totales
una(/\$\('#TotalDoc'\)\.val\(totales\.totalDoc\);/, "$('#TotalDoc').val(totales.totalDoc)");
s = s.replace("$('#TotalDoc').val(totales.totalDoc);", () => "$('#TotalDoc').val(totales.totalDoc);\n        actualizarAlertaExceso();");

if (crlf) s = s.replace(/\n/g, '\r\n');
fs.writeFileSync(p, s);
console.log('ok ' + p);
```

Ejecutar: `node <scratchpad>\parche-nc-js.js Web.UI/wwwroot/js/notascredito.js`. Luego verificar a mano: `node --check Web.UI/wwwroot/js/notascredito.js`; `grep -n "baseDisponible\|actualizarAlertaExceso" Web.UI/wwwroot/js/notascredito.js` muestra la declaración, la función, el uso en `cargarFacturasBase`, en el handler de `#btnCargarFacturaBase`, en `quitarFacturaBase` y en `pintarDetalle`; la URL en `cargarFacturasBase` es `'/NotasCredito/BuscarFacturasBase'`; no queda una segunda definición de `cargarFacturasBase`.

- [ ] **Step 4: Verificación y commit**

1. `node --check Web.UI/wwwroot/js/notascredito.js` sin errores.
2. `dotnet build Web.slnx` con el `BaseOutputPath` de la cabecera → 0 errores (Razor se compila en este SDK).
3. Cada `#id` usado por `notascredito.js` existe en `_Form.cshtml`/`Index.cshtml` (`grep -o "#[A-Za-z]*" Web.UI/wwwroot/js/notascredito.js | sort -u` contra los `id=` de las vistas; los ids de `asp-for` los genera el tag helper).
4. Tras el commit: `git worktree add --detach <scratchpad>\wt-check HEAD`, compilar allí con otro `BaseOutputPath`, `git worktree remove --force <scratchpad>\wt-check` (0 errores y 0 advertencias).

```bash
git add Web.UI/Controllers/NotasCreditoController.cs Web.UI/Views/NotasCredito/Index.cshtml Web.UI/Views/NotasCredito/_Form.cshtml Web.UI/wwwroot/js/notascredito.js
git commit -m "feat(notas-credito): pantalla de notas de credito de cliente con saldo acreditable de la factura base

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Pantalla Web de Notas de crédito de proveedor

**Files:**
- Create: `Web.UI/Controllers/NotasCreditoCompraController.cs`, `Web.UI/Views/NotasCreditoCompra/Index.cshtml`, `Web.UI/Views/NotasCreditoCompra/_Form.cshtml`, `Web.UI/wwwroot/js/notascreditocompra.js`

**Interfaces:**
- Consumes: archivos equivalentes de **notas de débito de proveedor** (`NotasDebitoCompraController.cs`, `Views/NotasDebitoCompra/*`, `notasdebitocompra.js`) como origen; `INotaCreditoCompraApiClient` y DTOs `Web.ApiClient.Dtos.NotaCreditoCompra` (Task 7); el script `parche-nc-js.js` del Task 8.
- Produces: rutas MVC `NotasCreditoCompra/*` equivalentes (acciones `BuscarFacturasBase`/`ObtenerFacturaBase` conservan el nombre y llaman a `ObtenerFacturasCompraBaseAsync`/`ObtenerFacturaCompraBaseAsync`), módulo `NotasCreditoCompra`.

**Precondición verificable:** Task 8 commiteado; `grep -c "NotasDebitoCompra" Web.UI/Views/NotasDebitoCompra/_Form.cshtml` > 0.

- [ ] **Step 1: Derivar y parchear**

Desde la raíz del repo `Web` (bash), con `nc` definida:

```bash
set -e
mkdir -p Web.UI/Views/NotasCreditoCompra
nc Web.UI/Controllers/NotasDebitoCompraController.cs Web.UI/Controllers/NotasCreditoCompraController.cs
nc Web.UI/Views/NotasDebitoCompra/Index.cshtml Web.UI/Views/NotasCreditoCompra/Index.cshtml
nc Web.UI/Views/NotasDebitoCompra/_Form.cshtml Web.UI/Views/NotasCreditoCompra/_Form.cshtml
nc Web.UI/wwwroot/js/notasdebitocompra.js Web.UI/wwwroot/js/notascreditocompra.js
sed -i -E 's/(CodigoObjNotaCredito = )"14"/\1"15"/' Web.UI/Controllers/NotasCreditoCompraController.cs
grep -rn "Debito\|debito\|Débito\|débito" Web.UI/Controllers/NotasCreditoCompraController.cs Web.UI/Views/NotasCreditoCompra Web.UI/wwwroot/js/notascreditocompra.js && echo FALLO || echo OK
grep -n 'CodigoObjNotaCredito =\|TipoSocio =' Web.UI/Controllers/NotasCreditoCompraController.cs     # "15" y "P"
grep -n 'TIPO_BASE =' Web.UI/wwwroot/js/notascreditocompra.js                                         # 13
grep -n 'Model.BaseTipo ==' Web.UI/Views/NotasCreditoCompra/_Form.cshtml                              # 13
```

Aplicar los mismos parches del Task 8 a los archivos del proveedor:

```bash
# aviso de exceso en la vista (mismo script del Task 8 Step 2, con la ruta del proveedor)
node - <<'EOF'
const fs = require('fs');
const p = 'Web.UI/Views/NotasCreditoCompra/_Form.cshtml';
let s = fs.readFileSync(p, 'utf8');
const crlf = s.includes('\r\n');
s = s.replace(/\r\n/g, '\n');
const m = s.match(/^( *)<span class="form-text" id="textoFacturaBase">.*<\/span>$/m);
if (!m) throw new Error('span textoFacturaBase no encontrado');
s = s.replace(m[0], m[0] + '\n' + m[1] + '<div id="alertaExcedeBase" class="alert alert-warning py-2 px-3 small mt-2 d-none"></div>');
if (crlf) s = s.replace(/\n/g, '\r\n');
fs.writeFileSync(p, s);
console.log('ok');
EOF
# parche del JS (el mismo script del Task 8)
node "C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\parche-nc-js.js" Web.UI/wwwroot/js/notascreditocompra.js
node --check Web.UI/wwwroot/js/notascreditocompra.js
```

- [ ] **Step 2: Revisar lo generado**

```bash
grep -n "'/NotasCreditoCompra/BuscarFacturasBase'" Web.UI/wwwroot/js/notascreditocompra.js    # la URL del proveedor en cargarFacturasBase
grep -rn "NotasCredito\"\|NotasCredito/" Web.UI/Controllers/NotasCreditoCompraController.cs Web.UI/Views/NotasCreditoCompra Web.UI/wwwroot/js/notascreditocompra.js   # vacio (solo NotasCreditoCompra)
grep -rnE "NotaCredito([^C]|$)" Web.UI/Controllers/NotasCreditoCompraController.cs Web.UI/Views/NotasCreditoCompra Web.UI/wwwroot/js/notascreditocompra.js   # vacio
grep -n "ObtenerFacturas\|ObtenerFactura" Web.UI/Controllers/NotasCreditoCompraController.cs   # llama a ...CompraBaseAsync del cliente
grep -n "Basada en la factura" Web.UI/wwwroot/js/notascreditocompra.js                          # "Basada en la factura de compra #"
```

Revisar a mano: (a) los ids del DOM del formulario (`formNotaCreditoCompra`, `selectSerieNotaCreditoCompra`, `tblDetalleNotaCreditoCompra`, `btnGuardarNotaCreditoCompra`, `btnCancelarDocNotaCreditoCompra`, `datosSeriesNotaCreditoCompra`, `datosPermisosNotasCreditoCompra`, `tblNotasCreditoCompra`) coinciden entre vista y JS (`grep -o "[A-Za-z]*NotaCreditoCompra[A-Za-z]*\|[A-Za-z]*NotasCreditoCompra[A-Za-z]*" <archivo> | sort -u` en cada uno); (b) `alertaExcedeBase` existe en la vista y el JS lo usa; (c) en el controller de proveedor la acción `BuscarFacturasBase`/`ObtenerFacturaBase` conserva el nombre, con `Accion.Agregar`.

- [ ] **Step 3: Verificación y commit**

`node --check Web.UI/wwwroot/js/notascreditocompra.js`; `dotnet build Web.slnx` → 0 errores.

```bash
git add Web.UI/Controllers/NotasCreditoCompraController.cs Web.UI/Views/NotasCreditoCompra/Index.cshtml Web.UI/Views/NotasCreditoCompra/_Form.cshtml Web.UI/wwwroot/js/notascreditocompra.js
git commit -m "feat(notas-credito): pantalla de notas de credito de proveedor con saldo acreditable de la factura de compra base

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Menú lateral

**Files:**
- Modify: `Web.UI/Views/Shared/_Layout.cshtml`

**Interfaces:**
- Consumes: módulos `NotasCredito` y `NotasCreditoCompra`, rutas `NotasCredito/Index` y `NotasCreditoCompra/Index` (Tasks 8-9); en el layout `Puede(string)`, `EsActivo(string)` y las listas `EsActivoVentas`/`EsActivoCompras`/`MuestraVentas`/`MuestraCompras`.

**Precondición verificable:** `grep -n 'NotasDebito' Web.UI/Views/Shared/_Layout.cshtml` muestra las 4 listas y los 2 bloques de enlace de notas de débito.

- [ ] **Step 1: Ampliar las listas y agregar los dos enlaces**

Desde la raíz del repo `Web` (bash; preserva EOL; falla si un ancla falta):

```bash
node - <<'EOF'
const fs = require('fs');
const p = 'Web.UI/Views/Shared/_Layout.cshtml';
let s = fs.readFileSync(p, 'utf8');
const crlf = s.includes('\r\n');
s = s.replace(/\r\n/g, '\n');

// 1) las cuatro listas
const antesVentas = (s.match(/"NotasDebito" \}/g) || []).length;
const antesCompras = (s.match(/"NotasDebitoCompra" \}/g) || []).length;
if (antesVentas !== 2 || antesCompras !== 2) throw new Error('listas inesperadas: ' + antesVentas + '/' + antesCompras);
s = s.replace(/"NotasDebito" \}/g, '"NotasDebito", "NotasCredito" }');
s = s.replace(/"NotasDebitoCompra" \}/g, '"NotasDebitoCompra", "NotasCreditoCompra" }');

// 2) enlaces: clonar cada bloque @if (Puede("NotasDebito...")) { ... } con nombres de credito
function clonarBloque(modulo) {
  const ini = s.indexOf('@if (Puede("' + modulo + '"))');
  if (ini < 0) throw new Error('bloque no encontrado: ' + modulo);
  const lineaIni = s.lastIndexOf('\n', ini) + 1;
  const finMarca = s.indexOf('\n                            }', ini);
  if (finMarca < 0) throw new Error('cierre de bloque no encontrado: ' + modulo);
  const fin = finMarca + '\n                            }'.length;
  const bloque = s.slice(lineaIni, fin);
  const nuevo = bloque
    .replace(/Débito/g, 'Crédito').replace(/débito/g, 'crédito')
    .replace(/Debito/g, 'Credito').replace(/debito/g, 'credito')
    .replace('fa-file-circle-plus', 'fa-file-circle-minus');
  s = s.slice(0, fin) + '\n\n' + nuevo + s.slice(fin);
}
clonarBloque('NotasDebito');
clonarBloque('NotasDebitoCompra');

if (crlf) s = s.replace(/\n/g, '\r\n');
fs.writeFileSync(p, s);
console.log('ok');
EOF
git diff --stat Web.UI/Views/Shared/_Layout.cshtml
grep -n 'NotasCredito' Web.UI/Views/Shared/_Layout.cshtml
```

Revisar el `git diff`: solo se modifican las 4 líneas de las listas y se agregan dos bloques `@if (Puede("NotasCredito")) { ... }` (después del de `NotasDebito`) y `@if (Puede("NotasCreditoCompra")) { ... }` (después del de `NotasDebitoCompra`), con `asp-controller="NotasCredito"` / `"NotasCreditoCompra"`, textos "Notas de crédito" y "Notas de crédito de compra" e ícono `fa-file-circle-minus`; mismo fin de línea e indentación que los bloques vecinos.

- [ ] **Step 2: Build y commit**

`dotnet build Web.slnx` → 0 errores.

```bash
git add Web.UI/Views/Shared/_Layout.cshtml
git commit -m "feat(notas-credito): enlaces de notas de credito en el menu de Ventas y Compras

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Fase 4 — Verificación

### Task 11: Verificación conjunta (sin commit)

**Files:** ninguno (task de verificación). Los defectos se **reportan** con la salida que falla; no se corrigen aquí.

**Precondición verificable:** Tasks 1-10 commiteados en ambos repos.

- [ ] **Step 1: API — build y suite completa**

`dotnet test API.sln` (PowerShell, con el `BaseOutputPath` de la cabecera) → 0 errores y todo verde; reportar el total (línea base 1137 + las nuevas).

- [ ] **Step 2: Web — árbol commiteado limpio**

En el repo Web: `git worktree add --detach <scratchpad>\wt-check HEAD`; compilar allí con un `BaseOutputPath` distinto → 0 errores y 0 advertencias; `git worktree remove --force <scratchpad>\wt-check`. `node --check` de `notascredito.js` y `notascreditocompra.js`.

- [ ] **Step 3: Humo de la API contra una base temporal (no toca `API_DB_TEST`)**

PowerShell, desde `C:\Users\migue\source\repos\angelm0508\API`:

```powershell
$pw = ([regex]::Match((Get-Content API.Service.WebApi\appsettings.json -Raw), 'Password=([^;]*)')).Groups[1].Value
$scratch = 'C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad'
# 1) base nueva desde el script completo
(Get-Content sql\crear-api-db-test-completo.sql -Raw) -replace 'API_DB_TEST','API_DB_SMOKE' | Set-Content "$scratch\smoke.sql" -Encoding UTF8
sqlcmd -S localhost -U sa -P $pw -C -b -f 65001 -i "$scratch\smoke.sql"
# 2) datos minimos: cliente, proveedor, articulo, factura de venta (total 16.8) y factura de compra (total 16.8)
sqlcmd -S localhost -U sa -P $pw -C -b -d API_DB_SMOKE -Q @"
INSERT INTO SocioNegocio(Codigo,Nombre,TipoSN,Serie,GrupoSN,Activo) VALUES('C0001','Cliente prueba','C',1,101,'S');
INSERT INTO SocioNegocio(Codigo,Nombre,TipoSN,Serie,GrupoSN,Activo) VALUES('P0001','Proveedor prueba','P',2,102,'S');
INSERT INTO Articulo(Codigo,Nombre,CodigoGrupo,CodigoGrpUnidadMedida,FabricanteEntry,Serie) VALUES('A1','Articulo',100,-1,-1,3);
INSERT INTO Factura(NumDoc,Serie,FechaDoc,FechaEmision,CodigoSN,NombreSN,MonedaDoc,TotalDoc) VALUES(1,10,GETDATE(),GETDATE(),'C0001','Cliente prueba','QTZ',16.8);
INSERT INTO FacturaDetalle(Entry,NoLinea,CodArticulo,Descripcion,Cantidad,Precio,PrctjeDesc,CodigoImpuesto,Impuesto,TotalLinea,CodAlmacen) VALUES(1,1,'A1','Tornillo',3,5,0,'IVA',1.8,16.8,'01');
INSERT INTO FacturaCompra(NumDoc,Serie,FechaDoc,FechaEmision,CodigoSN,NombreSN,MonedaDoc,TotalDoc) VALUES(1,26,GETDATE(),GETDATE(),'P0001','Proveedor prueba','QTZ',16.8);
INSERT INTO FacturaCompraDetalle(Entry,NoLinea,CodArticulo,Descripcion,Cantidad,Precio,PrctjeDesc,CodigoImpuesto,Impuesto,TotalLinea,CodAlmacen) VALUES(1,1,'A1','Tornillo',3,5,0,'IVA',1.8,16.8,'01');
INSERT INTO Usuario(ID,Password,Codigo,Nombre,SuperUsuario,Bloqueado,Eliminado) SELECT 2, Password, 'sinperm', 'Sin permisos', 'N', 'N', 'N' FROM Usuario WHERE ID = 1;
"@
# 3) levantar la API apuntando a esa base (otro puerto)
$env:ConnectionStrings__API_DB = "Server=localhost; Database=API_DB_SMOKE; User=sa; Password=$pw; TrustServerCertificate=True;"
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$api = Start-Process dotnet -ArgumentList 'run','--project','API.Service.WebApi','--no-launch-profile','--urls','http://localhost:5199','-p:BaseOutputPath=C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apismoke\' -PassThru -WindowStyle Hidden
# esperar hasta 60 s a que escuche (sondear, no dormir fijo)
1..30 | ForEach-Object { try { Invoke-RestMethod http://localhost:5199/api/Auth/login -Method Post -ContentType 'application/json' -Body '{}' -ErrorAction Stop } catch { if ($_.Exception.Response) { break } }; Start-Sleep -Seconds 2 }
```

Con la API arriba (mismo PowerShell) ejecutar y registrar cada respuesta:

```powershell
$base = 'http://localhost:5199'
$login = Invoke-RestMethod "$base/api/Auth/login" -Method Post -ContentType 'application/json' -Body '{"usuario":"admin","contrasena":"Admin123!"}'
$h = @{ Authorization = "Bearer $($login.dato.token)" }
function Nota($serie,$sn,$baseTipo,$entry,$precio,$monedaSn='QTZ') {
  @{ serie=$serie; codigoSn=$sn; monedaDoc=$monedaSn; baseTipo=$baseTipo; baseEntry=$entry; totalDoc=1; totalImp=999
     lineas=@(@{ descripcion='Ajuste de precio'; cantidad=1; precio=$precio; prctjeDesc=0; codigoImpuesto='IVA'; impuesto=999; totalLinea=999 }) } | ConvertTo-Json -Depth 5
}
# resumen y base: la factura 1 aparece con disponible 16.80
Invoke-RestMethod "$base/api/NotaCredito/FacturasBase/C0001" -Headers $h | ConvertTo-Json -Depth 5
Invoke-RestMethod "$base/api/NotaCredito/FacturaBase/1" -Headers $h | ConvertTo-Json -Depth 6
# 1) nota en base a la factura por 10 (+IVA = 11.20): valida y el servidor recalcula (impuesto 1.20, total 11.20)
$n1 = Invoke-RestMethod "$base/api/NotaCredito" -Method Post -Headers $h -ContentType 'application/json' -Body (Nota 37 'C0001' 6 1 10)
$n1 | ConvertTo-Json
Invoke-RestMethod "$base/api/NotaCredito/$($n1.dato)" -Headers $h | ConvertTo-Json            # tipoObjeto "8", totalDoc 11.20, totalImp 1.20
Invoke-RestMethod "$base/api/NotaCredito/$($n1.dato)/Lineas" -Headers $h | ConvertTo-Json -Depth 4   # impuesto 1.20, totalLinea 11.20
Invoke-RestMethod "$base/api/NotaCredito/FacturasBase/C0001" -Headers $h | ConvertTo-Json -Depth 5   # totalAcreditado 11.20, disponible 5.60
# 2) segunda nota por otros 11.20 excede el total de la factura (22.40 > 16.80) -> 400
try { Invoke-RestMethod "$base/api/NotaCredito" -Method Post -Headers $h -ContentType 'application/json' -Body (Nota 37 'C0001' 6 1 10) } catch { $_.ErrorDetails.Message }
# 3) cancelar la primera libera el monto y entonces la segunda entra
Invoke-RestMethod "$base/api/NotaCredito/$($n1.dato)" -Method Put -Headers $h -ContentType 'application/json' -Body '{"cancelado":"S"}' | ConvertTo-Json
$n2 = Invoke-RestMethod "$base/api/NotaCredito" -Method Post -Headers $h -ContentType 'application/json' -Body (Nota 37 'C0001' 6 1 10)
$n2 | ConvertTo-Json
# 4) eliminar sin cancelar -> 400; cancelar + eliminar -> 200
try { Invoke-RestMethod "$base/api/NotaCredito/$($n2.dato)" -Method Delete -Headers $h } catch { $_.ErrorDetails.Message }
Invoke-RestMethod "$base/api/NotaCredito/$($n1.dato)" -Method Delete -Headers $h | ConvertTo-Json
# 5) libre (sin base) con un monto grande: sin limite
Invoke-RestMethod "$base/api/NotaCredito" -Method Post -Headers $h -ContentType 'application/json' -Body (Nota 37 'C0001' -1 $null 100000) | ConvertTo-Json
# 6) socio equivocado (proveedor en nota de cliente) -> 400
try { Invoke-RestMethod "$base/api/NotaCredito" -Method Post -Headers $h -ContentType 'application/json' -Body (Nota 37 'P0001' -1 $null 10) } catch { $_.ErrorDetails.Message }
# 7) proveedor: nota en base a la factura de compra (serie 39) y limite
$p1 = Invoke-RestMethod "$base/api/NotaCreditoCompra" -Method Post -Headers $h -ContentType 'application/json' -Body (Nota 39 'P0001' 13 1 10)
$p1 | ConvertTo-Json
Invoke-RestMethod "$base/api/NotaCreditoCompra/FacturasCompraBase/P0001" -Headers $h | ConvertTo-Json -Depth 5
try { Invoke-RestMethod "$base/api/NotaCreditoCompra" -Method Post -Headers $h -ContentType 'application/json' -Body (Nota 39 'P0001' 13 1 10) } catch { $_.ErrorDetails.Message }
# 8) sin token -> 401; usuario sin permisos -> 403 en ambos
try { Invoke-RestMethod "$base/api/NotaCredito" } catch { $_.Exception.Response.StatusCode.value__ }
$l2 = Invoke-RestMethod "$base/api/Auth/login" -Method Post -ContentType 'application/json' -Body '{"usuario":"sinperm","contrasena":"Admin123!"}'
$h2 = @{ Authorization = "Bearer $($l2.dato.token)" }
try { Invoke-RestMethod "$base/api/NotaCredito" -Headers $h2 } catch { $_.Exception.Response.StatusCode.value__ }
try { Invoke-RestMethod "$base/api/NotaCreditoCompra" -Headers $h2 } catch { $_.Exception.Response.StatusCode.value__ }
```

Esperado: (1) la creación devuelve `resultado=true` con un `dato`; `NumDoc` autogenerado = 1; `tipoObjeto "8"`; aunque se mandó `totalImp=999` e `impuesto=999`, el servidor guarda `totalImp 1.20`, `totalDoc 11.20` y la línea con `impuesto 1.20`/`totalLinea 11.20`; (2) tras la primera, `FacturasBase` muestra `totalAcreditado 11.20` y `disponible 5.60`; (3) la segunda nota por 11.20 da **400** con `El total acreditado (22.40) excede el total de la factura base (16.80).`; (4) cancelar la primera da 200 y entonces la segunda se crea; (5) eliminar sin cancelar da 400 `Cancele el documento (Cancelado='S') antes de eliminarlo.`; cancelada, la eliminación da 200; (6) la nota libre de 100000 se crea; (7) un proveedor en la nota de cliente da 400 `El socio de negocio debe ser un cliente.`; (8) la nota de proveedor se crea con `tipoObjeto "15"`, el resumen de compra lista la factura con `disponible 5.60` y la segunda excede con `...factura de compra base (16.80).`; (9) sin token 401 y el usuario `sinperm` recibe 403 en ambos controllers.

Cierre obligatorio del humo (aunque falle algo):

```powershell
Stop-Process -Id $api.Id -Force -ErrorAction SilentlyContinue
Get-CimInstance Win32_Process | Where-Object { $_.ParentProcessId -eq $api.Id } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
sqlcmd -S localhost -U sa -P $pw -C -b -Q "ALTER DATABASE API_DB_SMOKE SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE API_DB_SMOKE"
```

y confirmar que el puerto 5199 ya no tiene listener, que `API_DB_SMOKE` no existe y que `API_DB_TEST` sigue sin tablas `NotaCredito*`. (`Remove-Item Env:` puede estar bloqueado en el entorno: la variable solo vive en la sesión de PowerShell que arrancó la API.)

- [ ] **Step 4: Estado de git**

`git -C API status --porcelain` → solo ` M API.Service.WebApi/appsettings.json`; `git -C Web status --porcelain` → solo los 4 archivos ajenos. Listar los commits nuevos de ambos repos (`git log --oneline` desde el commit previo a este plan: API `c5fb963`, Web `42d374b`).

- [ ] **Step 5: Checklist manual para el usuario (no automatizable aquí)**

Entregar al usuario esta lista (el humo no cubre la Web):

1. Ejecutar `sql/2026-10-05-notas-credito.sql` en `API_DB_TEST` con `sqlcmd -f 65001`, y reiniciar la depuración de la API y de la Web.
2. Como `admin`: ver "Notas de crédito" en Ventas y "Notas de crédito de compra" en Compras.
3. Crear una nota de cliente libre y otra en base a una factura (el selector muestra el disponible de cada factura y no lista las agotadas; "Cargar" precarga moneda, dirección y líneas; el aviso amarillo aparece si el total excede lo disponible; el servidor rechaza si se excede).
4. Crear una segunda nota que, sumada a la primera, exceda el total de la factura (debe fallar con el mensaje del límite); cancelar la primera y reintentar (debe pasar).
5. Crear una nota de proveedor (numeración automática y serie Manual).
6. Cancelar y eliminar una nota; intentar eliminar una sin cancelar muestra el mensaje de la API.
7. Con un usuario sin permisos no se ven los módulos; con `Ver` solamente en `NotasCredito` (más `Ver` en `SociosNegocio` y `NumeracionDocumento`) se ve la lista sin botones de Nuevo/Eliminar y se puede abrir el detalle en solo lectura.

- [ ] **Step 6: Integración**

No hay commit en este task. Usar `superpowers:finishing-a-development-branch` para presentar las opciones de integración al usuario una vez completadas las tareas anteriores. Sin push hasta aprobación.

---

## Riesgos y notas de cierre

- **Derivación mecánica:** los Tasks 2, 3, 6, 7, 8, 9 y 10 derivan archivos de notas de crédito de los de notas de débito con `sed`/`node`; cada tarea lista sus verificaciones (greps, pruebas, build). La Web no tiene pruebas automáticas: la cubren los greps, el humo de la API y el checklist manual.
- **Límite acreditable y concurrencia:** el chequeo corre dentro de una transacción `Serializable`; un interbloqueo puede devolver un error genérico que se resuelve reintentando (como en la gestión de usuarios).
- **Total de la factura base:** el límite usa el `TotalDoc` guardado de la factura (viene del cliente en las facturas existentes); no se recalcula la factura base.
- **Notas de débito:** siguen aceptando los totales del cliente (decisión previa); solo las notas de crédito los recalculan en el servidor.
- **Dependencia entre módulos:** un rol que solo tenga `NotasCredito` necesita además `Ver` sobre `SociosNegocio` y `NumeracionDocumento` para que funcionen los buscadores del formulario.
- **Despliegue:** ejecutar `sql/2026-10-05-notas-credito.sql` (con `-f 65001`) en cada base antes de arrancar la API; los módulos nuevos quedan sin asignar (acceso denegado por defecto) salvo para `SuperUsuario`.
