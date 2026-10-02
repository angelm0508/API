# Administración de permisos de usuario — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cada usuario tiene, por módulo, permiso para Ver/Agregar/Actualizar/Eliminar; sin `Ver`
el módulo no aparece en el menú ni es accesible; sin la acción correspondiente, el botón no
aparece y el endpoint la rechaza con 403 igual si se invoca directo.

**Architecture:** Una matriz `(sujeto, módulo) → 4 booleanos` (sujeto = Usuario o Plantilla).
Al login, la API emite claims `perm` en el JWT; la Web los copia a su cookie de sesión. Un
`IAuthorizationFilter` (`RequierePermisoAttribute`), replicado en API y Web, lee esos claims y
bloquea con 403 / redirige a "Acceso denegado" si falta el permiso. `SuperUsuario` bypassa todo.
Gestión de permisos restringida a `SuperUsuario`.

**Tech Stack:** .NET 7 (API, EF Core 7, AutoMapper, xUnit + Moq), .NET 8 (Web MVC), SQL Server
(`API_DB_TEST`), jQuery + Bootstrap 5 + SweetAlert2 (Web).

**Spec:** `docs/superpowers/specs/2026-10-01-permisos-usuario-design.md`

## Global Constraints

- Repos: API en `C:\Users\migue\source\repos\angelm0508\API` (rama `desarrollo`), Web en
  `C:\Users\migue\source\repos\angelm0508\Web` (rama `main`). Identidad de git: `panchoman08`.
- **Sin push hasta aprobación final del usuario.**
- `git add` con rutas de archivo explícitas — **nunca** `git add -A` ni `git add .`.
- `API.Service.WebApi/appsettings.json` puede aparecer modificado localmente — **nunca**
  commitearlo.
- Build/test con output redirigido fuera del árbol: API con
  `-p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\{apibuild|apitest}\"`,
  Web con `...\webbuild\`.
- `enum Accion { Ver, Agregar, Actualizar, Eliminar }` — mismos 4 valores, duplicado en
  `API.Domain.Interface` y en `Web.UI` (sin librería compartida entre repos).
- Claim `"perm"`, valor `"<CodigoModulo>:<letras>"`, letras ⊆ `"VAUE"` en ese orden
  (V=Ver, A=Agregar, U=Actualizar, E=Eliminar). Claim `"SuperUsuario"` ya existe (`"S"`/`"N"`).
  `SuperUsuario == "S"` bypassa todo chequeo sin mirar `"perm"`.
- Catálogo de 21 módulos (ver Task 1) — `CodigoModulo` es exactamente el nombre del controller
  sin el sufijo `Controller` (API) / nombre del controller MVC sin sufijo `Controller` y sin la
  `s` final de plural cuando el módulo del catálogo está en singular (ver tabla de Task 1 — el
  `Codigo` del módulo es el valor canónico, úsalo literal en ambos repos, no derives el nombre
  del controller).
- Controllers `...Detalle`/`Direccion...` usan el `CodigoModulo` del documento/entidad padre, no
  son un módulo aparte (ver tabla de mapeo completa en Task 7).
- `AuthController`/`CuentaController` (login/logout/AccesoDenegado), `HomeController` y
  `PreciosController` (Web) **no** llevan `[RequierePermiso]` — no son módulos del catálogo.
- Cada tarea termina con `dotnet build` + suite de tests en verde (o `dotnet build` limpio del
  lado Web, que no tiene tests automatizados) antes de pasar a la siguiente. Verificar el número
  exacto de tests en la baseline de la API con `dotnet test` sobre `HEAD` antes de la Task 1 — no
  asumir un número fijo.
- Cada tarea de API es un commit en `desarrollo`; cada tarea de Web es un commit en `main`.

---

## Fase 1 — API: núcleo del subsistema de permisos

### Task 1: Esquema (DDL), entidades EF y seed del catálogo de módulos

**Files:**
- Create: `API/sql/2026-10-01-permisos-usuario.sql`
- Create: `API.Domain.Entity/Models/Modulo.cs`
- Create: `API.Domain.Entity/Models/UsuarioPermiso.cs`
- Create: `API.Domain.Entity/Models/Plantilla.cs`
- Create: `API.Domain.Entity/Models/PlantillaPermisoDetalle.cs`
- Modify: `API.Domain.Entity/Models/ApiDbTestContext.cs` (4 `DbSet` + 4 bloques `OnModelCreating`)
- Modify: `API.Domain.Entity/Models/Usuario.cs` (agregar `ICollection<UsuarioPermiso> UsuarioPermisos`)

**Interfaces:**
- Produces: tablas `Modulo`, `UsuarioPermiso`, `Plantilla`, `PlantillaPermisoDetalle` + sus
  entidades EF, listas para que Task 2 las consuma vía `IRepositorioGenerico<T,K>`.

- [ ] **Step 1: Crear y ejecutar el script DDL**

`API/sql/2026-10-01-permisos-usuario.sql`:

```sql
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
```

Ejecutar contra `API_DB_TEST` (connection string real en `API.Service.WebApi/appsettings.json`,
no commitear ese archivo):

```powershell
sqlcmd -S localhost -U sa -P '#Integra1' -C -d API_DB_TEST -i "C:\Users\migue\source\repos\angelm0508\API\sql\2026-10-01-permisos-usuario.sql"
```

Esperado: 21 filas en el `SELECT` final.

- [ ] **Step 2: Crear las entidades EF**

`API.Domain.Entity/Models/Modulo.cs`:

```csharp
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class Modulo
{
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string Grupo { get; set; } = null!;

    public virtual ICollection<UsuarioPermiso> UsuarioPermisos { get; set; } = new List<UsuarioPermiso>();
    public virtual ICollection<PlantillaPermisoDetalle> PlantillaPermisoDetalles { get; set; } = new List<PlantillaPermisoDetalle>();
}
```

`API.Domain.Entity/Models/UsuarioPermiso.cs`:

```csharp
namespace API.Domain.Entity.Models;

public partial class UsuarioPermiso
{
    public int UsuarioId { get; set; }
    public string CodigoModulo { get; set; } = null!;
    public bool Ver { get; set; }
    public bool Agregar { get; set; }
    public bool Actualizar { get; set; }
    public bool Eliminar { get; set; }

    public virtual Usuario? UsuarioNavigation { get; set; }
    public virtual Modulo? ModuloNavigation { get; set; }
}
```

`API.Domain.Entity/Models/Plantilla.cs`:

```csharp
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class Plantilla
{
    public int Id { get; set; }
    public string Nombre { get; set; } = null!;

    public virtual ICollection<PlantillaPermisoDetalle> Detalles { get; set; } = new List<PlantillaPermisoDetalle>();
}
```

`API.Domain.Entity/Models/PlantillaPermisoDetalle.cs`:

```csharp
namespace API.Domain.Entity.Models;

public partial class PlantillaPermisoDetalle
{
    public int PlantillaId { get; set; }
    public string CodigoModulo { get; set; } = null!;
    public bool Ver { get; set; }
    public bool Agregar { get; set; }
    public bool Actualizar { get; set; }
    public bool Eliminar { get; set; }

    public virtual Plantilla? PlantillaNavigation { get; set; }
    public virtual Modulo? ModuloNavigation { get; set; }
}
```

En `API.Domain.Entity/Models/Usuario.cs`, agregar al final de la clase (antes de la llave de
cierre): `public virtual ICollection<UsuarioPermiso> UsuarioPermisos { get; set; } = new List<UsuarioPermiso>();`
y `using System.Collections.Generic;` si el archivo no lo tiene ya.

- [ ] **Step 3: Registrar las entidades en `ApiDbTestContext`**

Agregar, junto a los demás `DbSet` (orden alfabético, como ya están):

```csharp
public virtual DbSet<Modulo> Modulos { get; set; }
public virtual DbSet<UsuarioPermiso> UsuarioPermisos { get; set; }
public virtual DbSet<Plantilla> Plantillas { get; set; }
public virtual DbSet<PlantillaPermisoDetalle> PlantillaPermisoDetalles { get; set; }
```

Dentro de `OnModelCreating`, agregar 4 bloques nuevos (mismo estilo que el resto — ver
`modelBuilder.Entity<Pai>(entity => {...})` como referencia de formato):

```csharp
modelBuilder.Entity<Modulo>(entity =>
{
    entity.HasKey(e => e.Codigo).HasName("PK__Modulo");
    entity.ToTable("Modulo");
    entity.Property(e => e.Codigo).HasMaxLength(40);
    entity.Property(e => e.Nombre).HasMaxLength(100);
    entity.Property(e => e.Grupo).HasMaxLength(40);
});

modelBuilder.Entity<UsuarioPermiso>(entity =>
{
    entity.HasKey(e => new { e.UsuarioId, e.CodigoModulo }).HasName("pk_usuario_permiso");
    entity.ToTable("UsuarioPermiso");
    entity.Property(e => e.CodigoModulo).HasMaxLength(40);

    entity.HasOne(d => d.UsuarioNavigation).WithMany(p => p.UsuarioPermisos)
        .HasForeignKey(d => d.UsuarioId)
        .OnDelete(DeleteBehavior.ClientSetNull)
        .HasConstraintName("fk_usuario_permiso_usuario");

    entity.HasOne(d => d.ModuloNavigation).WithMany(p => p.UsuarioPermisos)
        .HasForeignKey(d => d.CodigoModulo)
        .OnDelete(DeleteBehavior.ClientSetNull)
        .HasConstraintName("fk_usuario_permiso_modulo");
});

modelBuilder.Entity<Plantilla>(entity =>
{
    entity.HasKey(e => e.Id).HasName("PK__Plantilla");
    entity.ToTable("Plantilla");
    entity.Property(e => e.Nombre).HasMaxLength(100);
});

modelBuilder.Entity<PlantillaPermisoDetalle>(entity =>
{
    entity.HasKey(e => new { e.PlantillaId, e.CodigoModulo }).HasName("pk_plantilla_permiso_detalle");
    entity.ToTable("PlantillaPermisoDetalle");
    entity.Property(e => e.CodigoModulo).HasMaxLength(40);

    entity.HasOne(d => d.PlantillaNavigation).WithMany(p => p.Detalles)
        .HasForeignKey(d => d.PlantillaId)
        .OnDelete(DeleteBehavior.ClientSetNull)
        .HasConstraintName("fk_plantilla_permiso_plantilla");

    entity.HasOne(d => d.ModuloNavigation).WithMany(p => p.PlantillaPermisoDetalles)
        .HasForeignKey(d => d.CodigoModulo)
        .OnDelete(DeleteBehavior.ClientSetNull)
        .HasConstraintName("fk_plantilla_permiso_modulo");
});
```

- [ ] **Step 4: Registrar los repos genéricos en `Startup.cs`**

Agregar junto a los demás (mismo patrón `AddTransient<IRepositorioGenerico<T,K>, XxxRepositorio>`,
ver Task siguiente para las clases `Repositorio`):

```csharp
services.AddTransient<IRepositorioGenerico<Modulo, string>, ModuloRepositorio>();
services.AddTransient<IRepositorioGenerico<UsuarioPermiso, (int UsuarioId, string CodigoModulo)>, UsuarioPermisoRepositorio>();
services.AddTransient<IRepositorioGenerico<Plantilla, int>, PlantillaRepositorio>();
services.AddTransient<IRepositorioGenerico<PlantillaPermisoDetalle, (int PlantillaId, string CodigoModulo)>, PlantillaPermisoDetalleRepositorio>();
```

- [ ] **Step 5: Crear los repositorios** (mismo patrón que `PedidoCompraDetalleRepositorio` —
  clave compuesta necesita `override ObtenerAsync` con `FindAsync` de dos partes)

`API.Infraestructure.Repository/ModuloRepositorio.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class ModuloRepositorio : RepositorioGenericoEfCore<Modulo, string>
    {
        public ModuloRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
```

`API.Infraestructure.Repository/PlantillaRepositorio.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class PlantillaRepositorio : RepositorioGenericoEfCore<Plantilla, int>
    {
        public PlantillaRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
```

`API.Infraestructure.Repository/UsuarioPermisoRepositorio.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class UsuarioPermisoRepositorio : RepositorioGenericoEfCore<UsuarioPermiso, (int UsuarioId, string CodigoModulo)>
    {
        public UsuarioPermisoRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        public override async Task<UsuarioPermiso?> ObtenerAsync((int UsuarioId, string CodigoModulo) id)
        {
            return await DbSet.FindAsync(id.UsuarioId, id.CodigoModulo);
        }
    }
}
```

`API.Infraestructure.Repository/PlantillaPermisoDetalleRepositorio.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class PlantillaPermisoDetalleRepositorio : RepositorioGenericoEfCore<PlantillaPermisoDetalle, (int PlantillaId, string CodigoModulo)>
    {
        public PlantillaPermisoDetalleRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        public override async Task<PlantillaPermisoDetalle?> ObtenerAsync((int PlantillaId, string CodigoModulo) id)
        {
            return await DbSet.FindAsync(id.PlantillaId, id.CodigoModulo);
        }
    }
}
```

- [ ] **Step 6: Build**

```powershell
cd C:\Users\migue\source\repos\angelm0508\API
dotnet build API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apibuild\"
```
Esperado: `0` Errores. No hay tests en este task (es solo esquema/infraestructura) — la suite
completa corre igual para confirmar que no rompió nada existente.

```powershell
dotnet test API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apitest\"
```
Esperado: igual a la baseline registrada antes de empezar (0 errores, mismo total).

- [ ] **Step 7: Commit**

```bash
git add sql/2026-10-01-permisos-usuario.sql API.Domain.Entity/Models/Modulo.cs API.Domain.Entity/Models/UsuarioPermiso.cs API.Domain.Entity/Models/Plantilla.cs API.Domain.Entity/Models/PlantillaPermisoDetalle.cs API.Domain.Entity/Models/ApiDbTestContext.cs API.Domain.Entity/Models/Usuario.cs API.Infraestructure.Repository/ModuloRepositorio.cs API.Infraestructure.Repository/PlantillaRepositorio.cs API.Infraestructure.Repository/UsuarioPermisoRepositorio.cs API.Infraestructure.Repository/PlantillaPermisoDetalleRepositorio.cs API.Service.WebApi/Startup.cs
git commit -m "feat(permisos): esquema, entidades y repos de Modulo/UsuarioPermiso/Plantilla

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 2: `IPermisoDomain` / `PermisoDomain` + `IModuloDomain` / `ModuloDomain`

**Files:**
- Create: `API.Domain.Interface/Accion.cs`
- Create: `API.Domain.Interface/PermisoModulo.cs`
- Create: `API.Domain.Interface/IPermisoDomain.cs`
- Create: `API.Domain.Interface/IModuloDomain.cs`
- Create: `API.Domain.Core/PermisoDomain.cs`
- Create: `API.Domain.Core/ModuloDomain.cs`
- Modify: `API.Service.WebApi/Startup.cs` (2 líneas de DI)
- Test: `API.Service.WebApi.Tests/Domain/PermisoDomainTests.cs`
- Test: `API.Service.WebApi.Tests/Domain/ModuloDomainTests.cs`

**Interfaces:**
- Produces: `enum Accion { Ver, Agregar, Actualizar, Eliminar }`;
  `record PermisoModulo(string CodigoModulo, bool Ver, bool Agregar, bool Actualizar, bool Eliminar)`;
  `IPermisoDomain.ObtenerMatrizUsuarioAsync(int usuarioId) : Task<IReadOnlyList<PermisoModulo>>`;
  `IPermisoDomain.GuardarMatrizUsuarioAsync(int usuarioId, IReadOnlyList<PermisoModulo> matriz) : Task`;
  `IPermisoDomain.ObtenerMatrizPlantillaAsync(int plantillaId) : Task<IReadOnlyList<PermisoModulo>>`;
  `IPermisoDomain.GuardarMatrizPlantillaAsync(int plantillaId, IReadOnlyList<PermisoModulo> matriz) : Task`;
  `IPermisoDomain.AplicarPlantillaAsync(int usuarioId, int plantillaId) : Task`;
  `IModuloDomain.ObtenerTodoAsync() : Task<IQueryable<Modulo>>`.

- [ ] **Step 1: Escribir los tests que fallan**

`API.Service.WebApi.Tests/Domain/ModuloDomainTests.cs`:

```csharp
using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class ModuloDomainTests
    {
        private readonly Mock<IRepositorioGenerico<Modulo, string>> _repo = new();
        private readonly ModuloDomain _domain;

        public ModuloDomainTests()
        {
            _domain = new ModuloDomain(_repo.Object);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveElCatalogo()
        {
            _repo.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<Modulo>
            {
                new() { Codigo = "Articulos", Nombre = "Artículos", Grupo = "Inventario" },
                new() { Codigo = "Usuarios", Nombre = "Usuarios", Grupo = "Administracion" },
            }.AsAsyncQueryable());

            var resultado = (await (await _domain.ObtenerTodoAsync()).ToListAsync());

            Assert.Equal(2, resultado.Count);
        }
    }
}
```

`API.Service.WebApi.Tests/Domain/PermisoDomainTests.cs`:

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
    public class PermisoDomainTests
    {
        private readonly Mock<IRepositorioGenerico<UsuarioPermiso, (int UsuarioId, string CodigoModulo)>> _repoUsuarioPermiso = new();
        private readonly Mock<IRepositorioGenerico<PlantillaPermisoDetalle, (int PlantillaId, string CodigoModulo)>> _repoPlantillaDetalle = new();
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly PermisoDomain _domain;

        public PermisoDomainTests()
        {
            _domain = new PermisoDomain(_repoUsuarioPermiso.Object, _repoPlantillaDetalle.Object, _tx.Object);
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>())).Returns<Func<Task<bool>>>(f => f());
        }

        [Fact]
        public async Task ObtenerMatrizUsuarioAsync_DevuelveLasFilasDelUsuario()
        {
            _repoUsuarioPermiso.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<UsuarioPermiso>
            {
                new() { UsuarioId = 1, CodigoModulo = "Articulos", Ver = true, Agregar = true },
                new() { UsuarioId = 2, CodigoModulo = "Articulos", Ver = true },
            }.AsAsyncQueryable());

            var matriz = await _domain.ObtenerMatrizUsuarioAsync(1);

            var fila = Assert.Single(matriz);
            Assert.Equal("Articulos", fila.CodigoModulo);
            Assert.True(fila.Ver);
            Assert.True(fila.Agregar);
            Assert.False(fila.Actualizar);
        }

        [Fact]
        public async Task ObtenerMatrizUsuarioAsync_SinFilas_DevuelveListaVacia()
        {
            _repoUsuarioPermiso.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<UsuarioPermiso>().AsAsyncQueryable());

            var matriz = await _domain.ObtenerMatrizUsuarioAsync(1);

            Assert.Empty(matriz);
        }

        [Fact]
        public async Task GuardarMatrizUsuarioAsync_BorraLasFilasViejasYInsertaLasNuevas()
        {
            _repoUsuarioPermiso.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<UsuarioPermiso>
            {
                new() { UsuarioId = 1, CodigoModulo = "Articulos", Ver = true },
                new() { UsuarioId = 1, CodigoModulo = "Almacenes", Ver = true },
            }.AsAsyncQueryable());
            _repoUsuarioPermiso.Setup(r => r.EliminarAsync(It.IsAny<(int, string)>())).ReturnsAsync(true);
            _repoUsuarioPermiso.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<UsuarioPermiso>())).Returns(Task.CompletedTask);

            var nuevaMatriz = new List<PermisoModulo>
            {
                new("Articulos", true, true, false, false),
                new("Usuarios", false, false, false, false), // las 4 en false -> no se inserta
            };

            await _domain.GuardarMatrizUsuarioAsync(1, nuevaMatriz);

            _repoUsuarioPermiso.Verify(r => r.EliminarAsync((1, "Articulos")), Times.Once);
            _repoUsuarioPermiso.Verify(r => r.EliminarAsync((1, "Almacenes")), Times.Once);
            _repoUsuarioPermiso.Verify(r => r.AgregarSinGuardarAsync(It.Is<UsuarioPermiso>(p => p.CodigoModulo == "Articulos" && p.Agregar)), Times.Once);
            _repoUsuarioPermiso.Verify(r => r.AgregarSinGuardarAsync(It.Is<UsuarioPermiso>(p => p.CodigoModulo == "Usuarios")), Times.Never);
        }

        [Fact]
        public async Task AplicarPlantillaAsync_CopiaLaMatrizDeLaPlantillaAlUsuario()
        {
            _repoUsuarioPermiso.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<UsuarioPermiso>
            {
                new() { UsuarioId = 1, CodigoModulo = "Almacenes", Ver = true },
            }.AsAsyncQueryable());
            _repoPlantillaDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PlantillaPermisoDetalle>
            {
                new() { PlantillaId = 5, CodigoModulo = "Articulos", Ver = true, Agregar = true, Actualizar = true, Eliminar = true },
            }.AsAsyncQueryable());
            _repoUsuarioPermiso.Setup(r => r.EliminarAsync(It.IsAny<(int, string)>())).ReturnsAsync(true);
            _repoUsuarioPermiso.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<UsuarioPermiso>())).Returns(Task.CompletedTask);

            await _domain.AplicarPlantillaAsync(1, 5);

            _repoUsuarioPermiso.Verify(r => r.EliminarAsync((1, "Almacenes")), Times.Once);
            _repoUsuarioPermiso.Verify(r => r.AgregarSinGuardarAsync(It.Is<UsuarioPermiso>(
                p => p.UsuarioId == 1 && p.CodigoModulo == "Articulos" && p.Ver && p.Agregar && p.Actualizar && p.Eliminar)), Times.Once);
        }
    }
}
```

- [ ] **Step 2: Ejecutar la suite y verificar que fallan por compilación**

```powershell
cd C:\Users\migue\source\repos\angelm0508\API
dotnet build API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apibuild\"
```
Esperado: error de compilación (`PermisoDomain`/`ModuloDomain`/`Accion`/`PermisoModulo` no
existen todavía).

- [ ] **Step 3: Crear `Accion` y `PermisoModulo`**

`API.Domain.Interface/Accion.cs`:

```csharp
namespace API.Domain.Interface
{
    public enum Accion { Ver, Agregar, Actualizar, Eliminar }
}
```

`API.Domain.Interface/PermisoModulo.cs`:

```csharp
namespace API.Domain.Interface
{
    public record PermisoModulo(string CodigoModulo, bool Ver, bool Agregar, bool Actualizar, bool Eliminar);
}
```

- [ ] **Step 4: Crear `IModuloDomain` / `ModuloDomain`**

`API.Domain.Interface/IModuloDomain.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IModuloDomain
    {
        Task<IQueryable<Modulo>> ObtenerTodoAsync();
    }
}
```

`API.Domain.Core/ModuloDomain.cs`:

```csharp
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;

namespace API.Domain.Core
{
    public class ModuloDomain : IModuloDomain
    {
        private readonly IRepositorioGenerico<Modulo, string> _repo;

        public ModuloDomain(IRepositorioGenerico<Modulo, string> repo)
        {
            _repo = repo;
        }

        public async Task<IQueryable<Modulo>> ObtenerTodoAsync()
        {
            return await _repo.ObtenerTodoAsync();
        }
    }
}
```

- [ ] **Step 5: Crear `IPermisoDomain` / `PermisoDomain`**

`API.Domain.Interface/IPermisoDomain.cs`:

```csharp
namespace API.Domain.Interface
{
    public interface IPermisoDomain
    {
        Task<IReadOnlyList<PermisoModulo>> ObtenerMatrizUsuarioAsync(int usuarioId);
        Task GuardarMatrizUsuarioAsync(int usuarioId, IReadOnlyList<PermisoModulo> matriz);
        Task<IReadOnlyList<PermisoModulo>> ObtenerMatrizPlantillaAsync(int plantillaId);
        Task GuardarMatrizPlantillaAsync(int plantillaId, IReadOnlyList<PermisoModulo> matriz);
        Task AplicarPlantillaAsync(int usuarioId, int plantillaId);
    }
}
```

`API.Domain.Core/PermisoDomain.cs`:

```csharp
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class PermisoDomain : IPermisoDomain
    {
        private readonly IRepositorioGenerico<UsuarioPermiso, (int UsuarioId, string CodigoModulo)> _repoUsuarioPermiso;
        private readonly IRepositorioGenerico<PlantillaPermisoDetalle, (int PlantillaId, string CodigoModulo)> _repoPlantillaDetalle;
        private readonly IEjecutorTransaccion _tx;

        public PermisoDomain(
            IRepositorioGenerico<UsuarioPermiso, (int UsuarioId, string CodigoModulo)> repoUsuarioPermiso,
            IRepositorioGenerico<PlantillaPermisoDetalle, (int PlantillaId, string CodigoModulo)> repoPlantillaDetalle,
            IEjecutorTransaccion tx)
        {
            _repoUsuarioPermiso = repoUsuarioPermiso;
            _repoPlantillaDetalle = repoPlantillaDetalle;
            _tx = tx;
        }

        public async Task<IReadOnlyList<PermisoModulo>> ObtenerMatrizUsuarioAsync(int usuarioId)
        {
            var filas = await (await _repoUsuarioPermiso.ObtenerTodoAsync())
                .Where(p => p.UsuarioId == usuarioId)
                .ToListAsync();
            return filas.Select(p => new PermisoModulo(p.CodigoModulo, p.Ver, p.Agregar, p.Actualizar, p.Eliminar)).ToList();
        }

        public async Task GuardarMatrizUsuarioAsync(int usuarioId, IReadOnlyList<PermisoModulo> matriz)
        {
            await _tx.EjecutarAsync(async () =>
            {
                var existentes = await (await _repoUsuarioPermiso.ObtenerTodoAsync())
                    .Where(p => p.UsuarioId == usuarioId)
                    .ToListAsync();
                foreach (var fila in existentes)
                    await _repoUsuarioPermiso.EliminarAsync((fila.UsuarioId, fila.CodigoModulo));

                foreach (var m in matriz.Where(m => m.Ver || m.Agregar || m.Actualizar || m.Eliminar))
                {
                    await _repoUsuarioPermiso.AgregarSinGuardarAsync(new UsuarioPermiso
                    {
                        UsuarioId = usuarioId,
                        CodigoModulo = m.CodigoModulo,
                        Ver = m.Ver,
                        Agregar = m.Agregar,
                        Actualizar = m.Actualizar,
                        Eliminar = m.Eliminar
                    });
                }
                return true;
            });
        }

        public async Task<IReadOnlyList<PermisoModulo>> ObtenerMatrizPlantillaAsync(int plantillaId)
        {
            var filas = await (await _repoPlantillaDetalle.ObtenerTodoAsync())
                .Where(p => p.PlantillaId == plantillaId)
                .ToListAsync();
            return filas.Select(p => new PermisoModulo(p.CodigoModulo, p.Ver, p.Agregar, p.Actualizar, p.Eliminar)).ToList();
        }

        public async Task GuardarMatrizPlantillaAsync(int plantillaId, IReadOnlyList<PermisoModulo> matriz)
        {
            await _tx.EjecutarAsync(async () =>
            {
                var existentes = await (await _repoPlantillaDetalle.ObtenerTodoAsync())
                    .Where(p => p.PlantillaId == plantillaId)
                    .ToListAsync();
                foreach (var fila in existentes)
                    await _repoPlantillaDetalle.EliminarAsync((fila.PlantillaId, fila.CodigoModulo));

                foreach (var m in matriz.Where(m => m.Ver || m.Agregar || m.Actualizar || m.Eliminar))
                {
                    await _repoPlantillaDetalle.AgregarSinGuardarAsync(new PlantillaPermisoDetalle
                    {
                        PlantillaId = plantillaId,
                        CodigoModulo = m.CodigoModulo,
                        Ver = m.Ver,
                        Agregar = m.Agregar,
                        Actualizar = m.Actualizar,
                        Eliminar = m.Eliminar
                    });
                }
                return true;
            });
        }

        public async Task AplicarPlantillaAsync(int usuarioId, int plantillaId)
        {
            var matrizPlantilla = await ObtenerMatrizPlantillaAsync(plantillaId);
            await GuardarMatrizUsuarioAsync(usuarioId, matrizPlantilla);
        }
    }
}
```

(`GuardarMatrizUsuarioAsync` ya corre dentro de `_tx.EjecutarAsync` -- `AplicarPlantillaAsync`
simplemente reusa ese método, sin anidar una segunda transacción porque `GuardarMatrizUsuarioAsync`
es la única que abre una.)

- [ ] **Step 6: Registrar en `Startup.cs`**

```csharp
services.AddTransient<IModuloDomain, ModuloDomain>();
services.AddTransient<IPermisoDomain, PermisoDomain>();
```

(`IEjecutorTransaccion` ya está registrado — lo usan todos los dominios de documento.)

- [ ] **Step 7: Ejecutar la suite y verificar que pasan**

```powershell
dotnet build API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apibuild\"
dotnet test API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apitest\" --filter "FullyQualifiedName~PermisoDomainTests|FullyQualifiedName~ModuloDomainTests"
```

- [ ] **Step 8: Commit**

```bash
git add API.Domain.Interface/Accion.cs API.Domain.Interface/PermisoModulo.cs API.Domain.Interface/IPermisoDomain.cs API.Domain.Interface/IModuloDomain.cs API.Domain.Core/PermisoDomain.cs API.Domain.Core/ModuloDomain.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Domain/PermisoDomainTests.cs API.Service.WebApi.Tests/Domain/ModuloDomainTests.cs
git commit -m "feat(permisos): PermisoDomain (matriz usuario/plantilla) y ModuloDomain

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 3: `IPlantillaDomain`, DTOs y capa de aplicación

**Files:**
- Create: `API.Domain.Interface/IPlantillaDomain.cs`
- Create: `API.Domain.Core/PlantillaDomain.cs`
- Create: `API.Application.DTO/permiso/ModuloDTO.cs`
- Create: `API.Application.DTO/permiso/PermisoModuloDTO.cs`
- Create: `API.Application.DTO/permiso/PlantillaDTO.cs`
- Create: `API.Application.DTO/permiso/PlantillaCrearDTO.cs`
- Create: `API.Application.DTO/permiso/PlantillaActualizarDTO.cs`
- Create: `API.Application.Interface/IPermisoApplication.cs`
- Create: `API.Application.Interface/IPlantillaApplication.cs`
- Create: `API.Application.Main/PermisoApplication.cs`
- Create: `API.Application.Main/PlantillaApplication.cs`
- Modify: `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Domain/PlantillaDomainTests.cs`
- Test: `API.Service.WebApi.Tests/Application/PermisoApplicationTests.cs`
- Test: `API.Service.WebApi.Tests/Application/PlantillaApplicationTests.cs`

**Interfaces:**
- Consumes: `IPermisoDomain`, `IModuloDomain`, `PermisoModulo` (Task 2).
- Produces: `IPermisoApplication.ObtenerModulosAsync()`, `.ObtenerMatrizUsuarioAsync(int)`,
  `.GuardarMatrizUsuarioAsync(int, List<PermisoModuloDTO>)`, `.AplicarPlantillaAsync(int, int)`,
  `.ObtenerMatrizPlantillaAsync(int)`, `.GuardarMatrizPlantillaAsync(int, List<PermisoModuloDTO>)`
  (todos `Task<Respuesta<T>>`); `IPlantillaApplication` con el CRUD estándar de `Plantilla`.

- [ ] **Step 1: Escribir los tests que fallan**

`API.Service.WebApi.Tests/Domain/PlantillaDomainTests.cs` (mismo patrón que
`PaisDomainTests`/cualquier dominio CRUD simple del proyecto):

```csharp
using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class PlantillaDomainTests
    {
        private readonly Mock<IRepositorioGenerico<Plantilla, int>> _repo = new();
        private readonly PlantillaDomain _domain;

        public PlantillaDomainTests()
        {
            _domain = new PlantillaDomain(_repo.Object);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveElIdAsignado()
        {
            _repo.Setup(r => r.InsertarAsync(It.IsAny<Plantilla>()))
                .ReturnsAsync((Plantilla p) => { p.Id = 7; return p; });

            var id = await _domain.InsertarAsync(new Plantilla { Nombre = "Vendedor" });

            Assert.Equal(7, id);
        }

        [Fact]
        public async Task ActualizarAsync_DelegaAlRepo()
        {
            _repo.Setup(r => r.ActualizarAsync(7, It.IsAny<Plantilla>())).ReturnsAsync(true);

            var resultado = await _domain.ActualizarAsync(7, new Plantilla { Nombre = "Vendedor 2" });

            Assert.True(resultado);
        }

        [Fact]
        public async Task EliminarAsync_DelegaAlRepo()
        {
            _repo.Setup(r => r.EliminarAsync(7)).ReturnsAsync(true);

            var resultado = await _domain.EliminarAsync(7);

            Assert.True(resultado);
        }
    }
}
```

`API.Service.WebApi.Tests/Application/PermisoApplicationTests.cs`:

```csharp
using API.Application.DTO.permiso;
using API.Application.Main;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class PermisoApplicationTests
    {
        private readonly Mock<IPermisoDomain> _permisoDomain = new();
        private readonly Mock<IModuloDomain> _moduloDomain = new();
        private readonly PermisoApplication _application;

        public PermisoApplicationTests()
        {
            _application = new PermisoApplication(_permisoDomain.Object, _moduloDomain.Object);
        }

        [Fact]
        public async Task ObtenerModulosAsync_Ok_MapeaElCatalogo()
        {
            _moduloDomain.Setup(m => m.ObtenerTodoAsync()).ReturnsAsync(new List<Modulo>
            {
                new() { Codigo = "Articulos", Nombre = "Artículos", Grupo = "Inventario" }
            }.AsQueryable());

            var respuesta = await _application.ObtenerModulosAsync();

            Assert.True(respuesta.Resultado);
            var m = Assert.Single(respuesta.Dato!);
            Assert.Equal("Articulos", m.Codigo);
        }

        [Fact]
        public async Task ObtenerMatrizUsuarioAsync_Ok_MapeaLaMatriz()
        {
            _permisoDomain.Setup(p => p.ObtenerMatrizUsuarioAsync(1))
                .ReturnsAsync(new List<PermisoModulo> { new("Articulos", true, false, false, false) });

            var respuesta = await _application.ObtenerMatrizUsuarioAsync(1);

            Assert.True(respuesta.Resultado);
            var fila = Assert.Single(respuesta.Dato!);
            Assert.Equal("Articulos", fila.CodigoModulo);
            Assert.True(fila.Ver);
        }

        [Fact]
        public async Task GuardarMatrizUsuarioAsync_Ok_DelegaAlDominio()
        {
            var matriz = new List<PermisoModuloDTO> { new() { CodigoModulo = "Articulos", Ver = true } };

            var respuesta = await _application.GuardarMatrizUsuarioAsync(1, matriz);

            Assert.True(respuesta.Resultado);
            _permisoDomain.Verify(p => p.GuardarMatrizUsuarioAsync(1, It.Is<List<PermisoModulo>>(
                l => l.Count == 1 && l[0].CodigoModulo == "Articulos" && l[0].Ver)), Times.Once);
        }

        [Fact]
        public async Task AplicarPlantillaAsync_Ok_DelegaAlDominio()
        {
            var respuesta = await _application.AplicarPlantillaAsync(1, 5);

            Assert.True(respuesta.Resultado);
            _permisoDomain.Verify(p => p.AplicarPlantillaAsync(1, 5), Times.Once);
        }

        [Fact]
        public async Task GuardarMatrizUsuarioAsync_ExcepcionDelDominio_DevuelveResultadoFalse()
        {
            _permisoDomain.Setup(p => p.GuardarMatrizUsuarioAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<PermisoModulo>>()))
                .ThrowsAsync(new Exception("fallo"));

            var respuesta = await _application.GuardarMatrizUsuarioAsync(1, new List<PermisoModuloDTO>());

            Assert.False(respuesta.Resultado);
            Assert.Equal("fallo", respuesta.Mensaje);
        }
    }
}
```

`API.Service.WebApi.Tests/Application/PlantillaApplicationTests.cs` (mismo patrón CRUD que
`PedidoCompraApplicationTests` u otro CRUD simple del proyecto):

```csharp
using API.Application.DTO.permiso;
using API.Application.Main;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class PlantillaApplicationTests
    {
        private readonly Mock<IPlantillaDomain> _domain = new();
        private readonly Mock<IMapper> _mapper = new();
        private readonly PlantillaApplication _application;

        public PlantillaApplicationTests()
        {
            _application = new PlantillaApplication(_domain.Object, _mapper.Object);
        }

        [Fact]
        public async Task InsertarAsync_Ok_DevuelveElId()
        {
            _mapper.Setup(m => m.Map<Plantilla>(It.IsAny<PlantillaCrearDTO>())).Returns(new Plantilla { Nombre = "Vendedor" });
            _domain.Setup(d => d.InsertarAsync(It.IsAny<Plantilla>())).ReturnsAsync(7);

            var respuesta = await _application.InsertarAsync(new PlantillaCrearDTO { Nombre = "Vendedor" });

            Assert.True(respuesta.Resultado);
            Assert.Equal(7, respuesta.Dato);
        }

        [Fact]
        public async Task EliminarAsync_ExcepcionDelDominio_DevuelveResultadoFalse()
        {
            _domain.Setup(d => d.EliminarAsync(It.IsAny<int>())).ThrowsAsync(new Exception("fallo"));

            var respuesta = await _application.EliminarAsync(7);

            Assert.False(respuesta.Resultado);
            Assert.Equal("fallo", respuesta.Mensaje);
        }
    }
}
```

- [ ] **Step 2: Ejecutar la suite y verificar que fallan por compilación**

```powershell
cd C:\Users\migue\source\repos\angelm0508\API
dotnet build API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apibuild\"
```

- [ ] **Step 3: Crear `IPlantillaDomain` / `PlantillaDomain`**

`API.Domain.Interface/IPlantillaDomain.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IPlantillaDomain
    {
        Task<int> InsertarAsync(Plantilla obj);
        Task<bool> ActualizarAsync(int id, Plantilla obj);
        Task<bool> EliminarAsync(int id);
        Task<Plantilla> ObtenerAsync(int id);
        Task<IQueryable<Plantilla>> ObtenerTodoAsync();
    }
}
```

`API.Domain.Core/PlantillaDomain.cs`:

```csharp
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;

namespace API.Domain.Core
{
    public class PlantillaDomain : IPlantillaDomain
    {
        private readonly IRepositorioGenerico<Plantilla, int> _repo;

        public PlantillaDomain(IRepositorioGenerico<Plantilla, int> repo)
        {
            _repo = repo;
        }

        public async Task<int> InsertarAsync(Plantilla obj)
        {
            var creado = await _repo.InsertarAsync(obj);
            return creado.Id;
        }

        public async Task<bool> ActualizarAsync(int id, Plantilla obj) => await _repo.ActualizarAsync(id, obj);
        public async Task<bool> EliminarAsync(int id) => await _repo.EliminarAsync(id);
        public async Task<Plantilla> ObtenerAsync(int id) => await _repo.ObtenerAsync(id);
        public async Task<IQueryable<Plantilla>> ObtenerTodoAsync() => await _repo.ObtenerTodoAsync();
    }
}
```

- [ ] **Step 4: Crear los DTOs**

`API.Application.DTO/permiso/ModuloDTO.cs`:

```csharp
namespace API.Application.DTO.permiso
{
    public class ModuloDTO
    {
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        public string Grupo { get; set; } = null!;
    }
}
```

`API.Application.DTO/permiso/PermisoModuloDTO.cs`:

```csharp
namespace API.Application.DTO.permiso
{
    public class PermisoModuloDTO
    {
        public string CodigoModulo { get; set; } = null!;
        public bool Ver { get; set; }
        public bool Agregar { get; set; }
        public bool Actualizar { get; set; }
        public bool Eliminar { get; set; }
    }
}
```

`API.Application.DTO/permiso/PlantillaDTO.cs`:

```csharp
namespace API.Application.DTO.permiso
{
    public class PlantillaDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
    }
}
```

`API.Application.DTO/permiso/PlantillaCrearDTO.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.permiso
{
    public class PlantillaCrearDTO
    {
        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string Nombre { get; set; } = null!;
    }
}
```

`API.Application.DTO/permiso/PlantillaActualizarDTO.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.permiso
{
    public class PlantillaActualizarDTO
    {
        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public string Nombre { get; set; } = null!;
    }
}
```

- [ ] **Step 5: Crear `IPermisoApplication` / `PermisoApplication`**

`API.Application.Interface/IPermisoApplication.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.permiso;

namespace API.Application.Interface
{
    public interface IPermisoApplication
    {
        Task<Respuesta<IEnumerable<ModuloDTO>>> ObtenerModulosAsync();
        Task<Respuesta<IEnumerable<PermisoModuloDTO>>> ObtenerMatrizUsuarioAsync(int usuarioId);
        Task<Respuesta<bool>> GuardarMatrizUsuarioAsync(int usuarioId, List<PermisoModuloDTO> matriz);
        Task<Respuesta<IEnumerable<PermisoModuloDTO>>> ObtenerMatrizPlantillaAsync(int plantillaId);
        Task<Respuesta<bool>> GuardarMatrizPlantillaAsync(int plantillaId, List<PermisoModuloDTO> matriz);
        Task<Respuesta<bool>> AplicarPlantillaAsync(int usuarioId, int plantillaId);
    }
}
```

`API.Application.Main/PermisoApplication.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.permiso;
using API.Application.Interface;
using API.Domain.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class PermisoApplication : IPermisoApplication
    {
        private readonly IPermisoDomain _permisoDomain;
        private readonly IModuloDomain _moduloDomain;

        public PermisoApplication(IPermisoDomain permisoDomain, IModuloDomain moduloDomain)
        {
            _permisoDomain = permisoDomain;
            _moduloDomain = moduloDomain;
        }

        public async Task<Respuesta<IEnumerable<ModuloDTO>>> ObtenerModulosAsync()
        {
            var respuesta = new Respuesta<IEnumerable<ModuloDTO>>();
            try
            {
                var queryable = await _moduloDomain.ObtenerTodoAsync();
                var modulos = await queryable.ToListAsync();
                respuesta.Dato = modulos.Select(m => new ModuloDTO { Codigo = m.Codigo, Nombre = m.Nombre, Grupo = m.Grupo }).ToList();
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<PermisoModuloDTO>>> ObtenerMatrizUsuarioAsync(int usuarioId)
        {
            var respuesta = new Respuesta<IEnumerable<PermisoModuloDTO>>();
            try
            {
                var matriz = await _permisoDomain.ObtenerMatrizUsuarioAsync(usuarioId);
                respuesta.Dato = MapearMatriz(matriz);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> GuardarMatrizUsuarioAsync(int usuarioId, List<PermisoModuloDTO> matriz)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                await _permisoDomain.GuardarMatrizUsuarioAsync(usuarioId, MapearDTOs(matriz));
                respuesta.Dato = true;
                respuesta.Resultado = true;
                respuesta.Mensaje = "Permisos actualizados correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<PermisoModuloDTO>>> ObtenerMatrizPlantillaAsync(int plantillaId)
        {
            var respuesta = new Respuesta<IEnumerable<PermisoModuloDTO>>();
            try
            {
                var matriz = await _permisoDomain.ObtenerMatrizPlantillaAsync(plantillaId);
                respuesta.Dato = MapearMatriz(matriz);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> GuardarMatrizPlantillaAsync(int plantillaId, List<PermisoModuloDTO> matriz)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                await _permisoDomain.GuardarMatrizPlantillaAsync(plantillaId, MapearDTOs(matriz));
                respuesta.Dato = true;
                respuesta.Resultado = true;
                respuesta.Mensaje = "Permisos de la plantilla actualizados correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> AplicarPlantillaAsync(int usuarioId, int plantillaId)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                await _permisoDomain.AplicarPlantillaAsync(usuarioId, plantillaId);
                respuesta.Dato = true;
                respuesta.Resultado = true;
                respuesta.Mensaje = "Plantilla aplicada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        private static List<PermisoModuloDTO> MapearMatriz(IReadOnlyList<PermisoModulo> matriz) =>
            matriz.Select(m => new PermisoModuloDTO { CodigoModulo = m.CodigoModulo, Ver = m.Ver, Agregar = m.Agregar, Actualizar = m.Actualizar, Eliminar = m.Eliminar }).ToList();

        private static List<PermisoModulo> MapearDTOs(List<PermisoModuloDTO> matriz) =>
            matriz.Select(m => new PermisoModulo(m.CodigoModulo, m.Ver, m.Agregar, m.Actualizar, m.Eliminar)).ToList();
    }
}
```

- [ ] **Step 6: Crear `IPlantillaApplication` / `PlantillaApplication`**

`API.Application.Interface/IPlantillaApplication.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.permiso;

namespace API.Application.Interface
{
    public interface IPlantillaApplication
    {
        Task<Respuesta<int>> InsertarAsync(PlantillaCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, PlantillaActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<PlantillaDTO>> ObtenerAsync(int id);
        Task<Respuesta<IEnumerable<PlantillaDTO>>> ObtenerTodoAsync();
    }
}
```

`API.Application.Main/PlantillaApplication.cs` (mismo patrón que `PedidoCompraApplication`,
mapeo manual en vez de AutoMapper para no agregar `CreateMap` nuevos solo para 2 campos):

```csharp
using API.Application.DTO;
using API.Application.DTO.permiso;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class PlantillaApplication : IPlantillaApplication
    {
        private readonly IPlantillaDomain _plantillaDomain;
        private readonly IMapper _mapper;

        public PlantillaApplication(IPlantillaDomain plantillaDomain, IMapper mapper)
        {
            _plantillaDomain = plantillaDomain;
            _mapper = mapper;
        }

        public async Task<Respuesta<int>> InsertarAsync(PlantillaCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var plantilla = _mapper.Map<Plantilla>(obj);
                respuesta.Dato = await _plantillaDomain.InsertarAsync(plantilla);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int id, PlantillaActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var plantilla = _mapper.Map<Plantilla>(obj);
                respuesta.Dato = await _plantillaDomain.ActualizarAsync(id, plantilla);
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
                respuesta.Dato = await _plantillaDomain.EliminarAsync(id);
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

        public async Task<Respuesta<PlantillaDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<PlantillaDTO>();
            try
            {
                var plantilla = await _plantillaDomain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<PlantillaDTO>(plantilla);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<PlantillaDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<PlantillaDTO>>();
            try
            {
                var queryable = await _plantillaDomain.ObtenerTodoAsync();
                var plantillas = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<PlantillaDTO>>(plantillas);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }
    }
}
```

`API.Transversal.Mapper/PerfilMapeo.cs` — agregar (junto a cualquier bloque, mismo estilo que
`CreateMap<PedidoCompraCrearDTO, PedidoCompra>()`):

```csharp
CreateMap<Plantilla, PlantillaDTO>();
CreateMap<PlantillaCrearDTO, Plantilla>();
CreateMap<PlantillaActualizarDTO, Plantilla>();
```

- [ ] **Step 7: Registrar en `Startup.cs`**

```csharp
services.AddTransient<IPlantillaDomain, PlantillaDomain>();
services.AddTransient<IPlantillaApplication, PlantillaApplication>();
services.AddTransient<IPermisoApplication, PermisoApplication>();
```

- [ ] **Step 8: Ejecutar la suite y verificar que pasan**

```powershell
dotnet build API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apibuild\"
dotnet test API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apitest\" --filter "FullyQualifiedName~Plantilla|FullyQualifiedName~Permiso"
```

- [ ] **Step 9: Commit**

```bash
git add API.Domain.Interface/IPlantillaDomain.cs API.Domain.Core/PlantillaDomain.cs API.Application.DTO/permiso/ API.Application.Interface/IPermisoApplication.cs API.Application.Interface/IPlantillaApplication.cs API.Application.Main/PermisoApplication.cs API.Application.Main/PlantillaApplication.cs API.Transversal.Mapper/PerfilMapeo.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Domain/PlantillaDomainTests.cs API.Service.WebApi.Tests/Application/PermisoApplicationTests.cs API.Service.WebApi.Tests/Application/PlantillaApplicationTests.cs
git commit -m "feat(permisos): capa de aplicacion de permisos y CRUD de Plantilla

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 4: `RequierePermisoAttribute` (API)

**Files:**
- Create: `API.Service.WebApi/Authorization/RequierePermisoAttribute.cs`
- Test: `API.Service.WebApi.Tests/Authorization/RequierePermisoAttributeTests.cs`

**Interfaces:**
- Consumes: `Accion` (Task 2).
- Produces: `[RequierePermiso(string codigoModulo, Accion accion)]`, utilizable en cualquier acción
  de cualquier controller (`AuthorizationFilterContext.Result` queda en `403` si falta el permiso).

- [ ] **Step 1: Escribir los tests que fallan**

```csharp
using System.Security.Claims;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace API.Service.WebApi.Tests.Authorization
{
    public class RequierePermisoAttributeTests
    {
        private static AuthorizationFilterContext Contexto(params Claim[] claims)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
        }

        [Fact]
        public void OnAuthorization_SuperUsuario_NoBloquea()
        {
            var ctx = Contexto(new Claim("SuperUsuario", "S"));
            new RequierePermisoAttribute("Articulos", Accion.Eliminar).OnAuthorization(ctx);

            Assert.Null(ctx.Result);
        }

        [Fact]
        public void OnAuthorization_ConLaLetraPresente_NoBloquea()
        {
            var ctx = Contexto(new Claim("SuperUsuario", "N"), new Claim("perm", "Articulos:VAUE"));
            new RequierePermisoAttribute("Articulos", Accion.Agregar).OnAuthorization(ctx);

            Assert.Null(ctx.Result);
        }

        [Fact]
        public void OnAuthorization_SinLaLetra_Bloquea403()
        {
            var ctx = Contexto(new Claim("SuperUsuario", "N"), new Claim("perm", "Articulos:V"));
            new RequierePermisoAttribute("Articulos", Accion.Eliminar).OnAuthorization(ctx);

            var resultado = Assert.IsType<ObjectResult>(ctx.Result);
            Assert.Equal(StatusCodes.Status403Forbidden, resultado.StatusCode);
        }

        [Fact]
        public void OnAuthorization_SinClaimParaEseModulo_Bloquea403()
        {
            var ctx = Contexto(new Claim("SuperUsuario", "N"), new Claim("perm", "Almacenes:VAUE"));
            new RequierePermisoAttribute("Articulos", Accion.Ver).OnAuthorization(ctx);

            Assert.IsType<ObjectResult>(ctx.Result);
        }

        [Fact]
        public void OnAuthorization_SinClaimSuperUsuario_SeTrataComoN()
        {
            var ctx = Contexto(new Claim("perm", "Articulos:V"));
            new RequierePermisoAttribute("Articulos", Accion.Ver).OnAuthorization(ctx);

            Assert.Null(ctx.Result);
        }
    }
}
```

- [ ] **Step 2: Ejecutar la suite y verificar que falla por compilación**

```powershell
cd C:\Users\migue\source\repos\angelm0508\API
dotnet build API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apibuild\"
```

- [ ] **Step 3: Implementar el atributo**

`API.Service.WebApi/Authorization/RequierePermisoAttribute.cs`:

```csharp
using API.Domain.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace API.Service.WebApi.Authorization
{
    [AttributeUsage(AttributeTargets.Method)]
    public class RequierePermisoAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string _codigoModulo;
        private readonly Accion _accion;

        public RequierePermisoAttribute(string codigoModulo, Accion accion)
        {
            _codigoModulo = codigoModulo;
            _accion = accion;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            if (user.FindFirst("SuperUsuario")?.Value == "S") return;

            var letra = _accion switch
            {
                Accion.Ver => 'V',
                Accion.Agregar => 'A',
                Accion.Actualizar => 'U',
                Accion.Eliminar => 'E',
                _ => ' '
            };

            var tienePermiso = user.FindAll("perm")
                .Select(c => c.Value.Split(':'))
                .Where(p => p.Length == 2 && p[0] == _codigoModulo)
                .Any(p => p[1].Contains(letra));

            if (!tienePermiso)
            {
                context.Result = new ObjectResult(new API.Application.DTO.Respuesta<object>
                {
                    Mensaje = "No tenés permiso para realizar esta acción."
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
        }
    }
}
```

(`using Microsoft.AspNetCore.Http;` para `StatusCodes` si el `using` de Mvc no lo trae ya en este
proyecto — agregarlo si el build se queja.)

- [ ] **Step 4: Ejecutar la suite y verificar que pasa**

```powershell
dotnet build API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apibuild\"
dotnet test API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apitest\" --filter "FullyQualifiedName~RequierePermisoAttributeTests"
```

- [ ] **Step 5: Commit**

```bash
git add API.Service.WebApi/Authorization/RequierePermisoAttribute.cs API.Service.WebApi.Tests/Authorization/RequierePermisoAttributeTests.cs
git commit -m "feat(permisos): RequierePermisoAttribute (API)

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 5: Endpoints de administración (`ModuloController`, `UsuarioPermisoController`, `PlantillaController`)

**Files:**
- Create: `API.Service.WebApi/Controllers/ModuloController.cs`
- Create: `API.Service.WebApi/Controllers/UsuarioPermisoController.cs`
- Create: `API.Service.WebApi/Controllers/PlantillaController.cs`
- Test: `API.Service.WebApi.Tests/Controllers/ModuloControllerTests.cs`
- Test: `API.Service.WebApi.Tests/Controllers/UsuarioPermisoControllerTests.cs`
- Test: `API.Service.WebApi.Tests/Controllers/PlantillaControllerTests.cs`

**Interfaces:**
- Consumes: `RequierePermisoAttribute` (Task 4), `IPermisoApplication`/`IPlantillaApplication` (Task 3).
- Produces: `GET api/Modulo`; `GET/PUT api/UsuarioPermiso/{usuarioId}`,
  `POST api/UsuarioPermiso/{usuarioId}/AplicarPlantilla/{plantillaId}`; CRUD `api/Plantilla` +
  `GET/PUT api/Plantilla/{id}/Permisos`.

Todas las acciones de estos 3 controllers llevan `[RequierePermiso("Usuarios", Accion.X)]` — en la
práctica solo `SuperUsuario` tiene ese módulo asignado (nunca se le asigna a nadie más desde la
Web, ver Fase 4). Las acciones de escritura de `UsuarioPermisoController`/`PlantillaController`
**además** verifican `SuperUsuario == "S"` de forma explícita al principio del método, como
defensa en profundidad (spec §2) ante el caso de que alguien manipule `UsuarioPermiso` directo en
la base de datos y se autoasigne el módulo `Usuarios`.

- [ ] **Step 1: Escribir los tests que fallan**

`API.Service.WebApi.Tests/Controllers/ModuloControllerTests.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.permiso;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class ModuloControllerTests
    {
        private readonly Mock<IPermisoApplication> _applicationMock = new();
        private readonly ModuloController _controller;

        public ModuloControllerTests()
        {
            _controller = new ModuloController(_applicationMock.Object);
        }

        [Fact]
        public async Task Obtener_Ok_DevuelveOk200()
        {
            _applicationMock.Setup(a => a.ObtenerModulosAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<ModuloDTO>> { Resultado = true, Dato = new List<ModuloDTO>() });

            var resultado = await _controller.Obtener();

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Obtener_Falla_DevuelveBadRequest()
        {
            _applicationMock.Setup(a => a.ObtenerModulosAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<ModuloDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Obtener();

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }
    }
}
```

`API.Service.WebApi.Tests/Controllers/UsuarioPermisoControllerTests.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.permiso;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class UsuarioPermisoControllerTests
    {
        private readonly Mock<IPermisoApplication> _applicationMock = new();
        private readonly UsuarioPermisoController _controller;

        public UsuarioPermisoControllerTests()
        {
            _controller = new UsuarioPermisoController(_applicationMock.Object);
        }

        [Fact]
        public async Task Obtener_Ok_DevuelveOk200()
        {
            _applicationMock.Setup(a => a.ObtenerMatrizUsuarioAsync(1))
                .ReturnsAsync(new Respuesta<IEnumerable<PermisoModuloDTO>> { Resultado = true, Dato = new List<PermisoModuloDTO>() });

            var resultado = await _controller.Obtener(1);

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Guardar_Ok_DevuelveOk200()
        {
            _applicationMock.Setup(a => a.GuardarMatrizUsuarioAsync(1, It.IsAny<List<PermisoModuloDTO>>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.Guardar(1, new List<PermisoModuloDTO>());

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task AplicarPlantilla_Ok_DevuelveOk200()
        {
            _applicationMock.Setup(a => a.AplicarPlantillaAsync(1, 5))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.AplicarPlantilla(1, 5);

            Assert.IsType<OkObjectResult>(resultado.Result);
        }
    }
}
```

`API.Service.WebApi.Tests/Controllers/PlantillaControllerTests.cs` (patrón CRUD estándar del
proyecto — un test de `Obtener` 200/404 y uno de `Permisos` alcanza, el resto del CRUD ya está
cubierto en forma por los tests de cualquier controller CRUD existente):

```csharp
using API.Application.DTO;
using API.Application.DTO.permiso;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class PlantillaControllerTests
    {
        private readonly Mock<IPlantillaApplication> _applicationMock = new();
        private readonly Mock<IPermisoApplication> _permisoApplicationMock = new();
        private readonly PlantillaController _controller;

        public PlantillaControllerTests()
        {
            _controller = new PlantillaController(_applicationMock.Object, _permisoApplicationMock.Object);
        }

        [Fact]
        public async Task Obtener_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<PlantillaDTO> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Obtener(1);

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Obtener_DevuelveNotFound_CuandoDatoEsNull()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<PlantillaDTO> { Resultado = true, Dato = null });

            var resultado = await _controller.Obtener(1);

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Permisos_Ok_DevuelveOk200()
        {
            _permisoApplicationMock.Setup(a => a.ObtenerMatrizPlantillaAsync(5))
                .ReturnsAsync(new Respuesta<IEnumerable<PermisoModuloDTO>> { Resultado = true, Dato = new List<PermisoModuloDTO>() });

            var resultado = await _controller.Permisos(5);

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task GuardarPermisos_Ok_DevuelveOk200()
        {
            _permisoApplicationMock.Setup(a => a.GuardarMatrizPlantillaAsync(5, It.IsAny<List<PermisoModuloDTO>>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.GuardarPermisos(5, new List<PermisoModuloDTO>());

            Assert.IsType<OkObjectResult>(resultado.Result);
        }
    }
}
```

- [ ] **Step 2: Ejecutar la suite y verificar que fallan por compilación**

```powershell
cd C:\Users\migue\source\repos\angelm0508\API
dotnet build API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apibuild\"
```

- [ ] **Step 3: Crear `ModuloController`**

```csharp
using API.Application.DTO;
using API.Application.DTO.permiso;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Modulo")]
    public class ModuloController : ControllerBase
    {
        private readonly IPermisoApplication _permisoApplication;

        public ModuloController(IPermisoApplication permisoApplication)
        {
            _permisoApplication = permisoApplication;
        }

        [HttpGet]
        [RequierePermiso("Usuarios", Accion.Ver)]
        public async Task<ActionResult<Respuesta<IEnumerable<ModuloDTO>>>> Obtener()
        {
            var res = await _permisoApplication.ObtenerModulosAsync();
            return res.Resultado ? Ok(res) : BadRequest(res);
        }
    }
}
```

- [ ] **Step 4: Crear `UsuarioPermisoController`**

```csharp
using API.Application.DTO;
using API.Application.DTO.permiso;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/UsuarioPermiso")]
    public class UsuarioPermisoController : ControllerBase
    {
        private readonly IPermisoApplication _permisoApplication;

        public UsuarioPermisoController(IPermisoApplication permisoApplication)
        {
            _permisoApplication = permisoApplication;
        }

        [HttpGet("{usuarioId:int}")]
        [RequierePermiso("Usuarios", Accion.Ver)]
        public async Task<ActionResult<Respuesta<IEnumerable<PermisoModuloDTO>>>> Obtener([FromRoute] int usuarioId)
        {
            var res = await _permisoApplication.ObtenerMatrizUsuarioAsync(usuarioId);
            return res.Resultado ? Ok(res) : BadRequest(res);
        }

        [HttpPut("{usuarioId:int}")]
        [RequierePermiso("Usuarios", Accion.Actualizar)]
        public async Task<ActionResult<Respuesta<bool>>> Guardar([FromRoute] int usuarioId, [FromBody] List<PermisoModuloDTO> matriz)
        {
            if (User.FindFirst("SuperUsuario")?.Value != "S")
                return Forbid();

            var res = await _permisoApplication.GuardarMatrizUsuarioAsync(usuarioId, matriz);
            return res.Resultado ? Ok(res) : BadRequest(res);
        }

        [HttpPost("{usuarioId:int}/AplicarPlantilla/{plantillaId:int}")]
        [RequierePermiso("Usuarios", Accion.Actualizar)]
        public async Task<ActionResult<Respuesta<bool>>> AplicarPlantilla([FromRoute] int usuarioId, [FromRoute] int plantillaId)
        {
            if (User.FindFirst("SuperUsuario")?.Value != "S")
                return Forbid();

            var res = await _permisoApplication.AplicarPlantillaAsync(usuarioId, plantillaId);
            return res.Resultado ? Ok(res) : BadRequest(res);
        }
    }
}
```

- [ ] **Step 5: Crear `PlantillaController`**

```csharp
using API.Application.DTO;
using API.Application.DTO.permiso;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Plantilla")]
    public class PlantillaController : ControllerBase
    {
        private readonly IPlantillaApplication _plantillaApplication;
        private readonly IPermisoApplication _permisoApplication;

        public PlantillaController(IPlantillaApplication plantillaApplication, IPermisoApplication permisoApplication)
        {
            _plantillaApplication = plantillaApplication;
            _permisoApplication = permisoApplication;
        }

        [HttpGet("{id:int}")]
        [RequierePermiso("Usuarios", Accion.Ver)]
        public async Task<ActionResult<Respuesta<PlantillaDTO>>> Obtener([FromRoute] int id)
        {
            var plantilla = await _plantillaApplication.ObtenerAsync(id);
            if (!plantilla.Resultado) return BadRequest(plantilla);
            if (plantilla.Dato == null)
            {
                plantilla.Resultado = false;
                plantilla.Mensaje = "El código de la plantilla no se encontró.";
                return NotFound(plantilla);
            }
            return Ok(plantilla);
        }

        [HttpGet]
        [RequierePermiso("Usuarios", Accion.Ver)]
        public async Task<ActionResult<Respuesta<IEnumerable<PlantillaDTO>>>> ObtenerTodoAsync()
        {
            var plantillas = await _plantillaApplication.ObtenerTodoAsync();
            return plantillas.Resultado ? Ok(plantillas) : BadRequest(plantillas);
        }

        [HttpPost]
        [RequierePermiso("Usuarios", Accion.Agregar)]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] PlantillaCrearDTO obj)
        {
            if (User.FindFirst("SuperUsuario")?.Value != "S") return Forbid();
            var insert = await _plantillaApplication.InsertarAsync(obj);
            return insert.Resultado ? Ok(insert) : BadRequest(insert);
        }

        [HttpPut("{id:int}")]
        [RequierePermiso("Usuarios", Accion.Actualizar)]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] PlantillaActualizarDTO obj)
        {
            if (User.FindFirst("SuperUsuario")?.Value != "S") return Forbid();
            var update = await _plantillaApplication.ActualizarAsync(id, obj);
            return update.Resultado ? Ok(update) : BadRequest(update);
        }

        [HttpDelete("{id:int}")]
        [RequierePermiso("Usuarios", Accion.Eliminar)]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            if (User.FindFirst("SuperUsuario")?.Value != "S") return Forbid();
            var delete = await _plantillaApplication.EliminarAsync(id);
            return delete.Resultado ? Ok(delete) : BadRequest(delete);
        }

        [HttpGet("{id:int}/Permisos")]
        [RequierePermiso("Usuarios", Accion.Ver)]
        public async Task<ActionResult<Respuesta<IEnumerable<PermisoModuloDTO>>>> Permisos([FromRoute] int id)
        {
            var res = await _permisoApplication.ObtenerMatrizPlantillaAsync(id);
            return res.Resultado ? Ok(res) : BadRequest(res);
        }

        [HttpPut("{id:int}/Permisos")]
        [RequierePermiso("Usuarios", Accion.Actualizar)]
        public async Task<ActionResult<Respuesta<bool>>> GuardarPermisos([FromRoute] int id, [FromBody] List<PermisoModuloDTO> matriz)
        {
            if (User.FindFirst("SuperUsuario")?.Value != "S") return Forbid();
            var res = await _permisoApplication.GuardarMatrizPlantillaAsync(id, matriz);
            return res.Resultado ? Ok(res) : BadRequest(res);
        }
    }
}
```

- [ ] **Step 6: Ejecutar la suite completa**

```powershell
dotnet build API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apibuild\"
dotnet test API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apitest\"
```
Esperado: `0` Errores, suite completa en verde (baseline + todo lo agregado en Tasks 1-5).

- [ ] **Step 7: Commit**

```bash
git add API.Service.WebApi/Controllers/ModuloController.cs API.Service.WebApi/Controllers/UsuarioPermisoController.cs API.Service.WebApi/Controllers/PlantillaController.cs API.Service.WebApi.Tests/Controllers/ModuloControllerTests.cs API.Service.WebApi.Tests/Controllers/UsuarioPermisoControllerTests.cs API.Service.WebApi.Tests/Controllers/PlantillaControllerTests.cs
git commit -m "feat(permisos): endpoints Modulo, UsuarioPermiso y Plantilla

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 6: `TokenService` emite claims `perm` + decorar `UsuarioController`

**Files:**
- Modify: `API.Application.Interface/ITokenService.cs`
- Modify: `API.Application.Main/TokenService.cs`
- Modify: `API.Application.Main/AuthApplication.cs`
- Modify: `API.Service.WebApi/Controllers/UsuarioController.cs`
- Test: `API.Service.WebApi.Tests/Application/TokenServiceTests.cs` (crear)

**Interfaces:**
- Consumes: `IPermisoDomain.ObtenerMatrizUsuarioAsync`, `IModuloDomain.ObtenerTodoAsync` (Task 2).
- Produces: `ITokenService.GenerarTokenAsync(Usuario) : Task<(string Token, DateTime ExpiraEn)>`
  (reemplaza la firma síncrona anterior).

- [ ] **Step 1: Escribir los tests que fallan**

`API.Service.WebApi.Tests/Application/TokenServiceTests.cs` — usa `Microsoft.Extensions.Configuration`
con un diccionario en memoria (mismo patrón que cualquier test que necesite `IConfiguration` sin
un `appsettings.json` real):

```csharp
using System.IdentityModel.Tokens.Jwt;
using API.Application.Main;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class TokenServiceTests
    {
        private readonly Mock<IPermisoDomain> _permisoDomain = new();
        private readonly Mock<IModuloDomain> _moduloDomain = new();
        private readonly TokenService _service;

        public TokenServiceTests()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:Key", "una-clave-de-prueba-bien-larga-para-firmar-el-token-1234567890" },
                { "Jwt:Issuer", "test" },
                { "Jwt:Audience", "test" },
                { "Jwt:ExpirationMinutes", "60" }
            }).Build();
            _service = new TokenService(config, _permisoDomain.Object, _moduloDomain.Object);
        }

        private static List<Claim> LeerClaims(string token) =>
            new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.ToList();

        [Fact]
        public async Task GenerarTokenAsync_SuperUsuario_NoEmiteClaimsPerm()
        {
            var usuario = new Usuario { Id = 1, Codigo = "admin", SuperUsuario = "S" };

            var (token, _) = await _service.GenerarTokenAsync(usuario);

            var claims = LeerClaims(token);
            Assert.Contains(claims, c => c.Type == "SuperUsuario" && c.Value == "S");
            Assert.DoesNotContain(claims, c => c.Type == "perm");
            _permisoDomain.Verify(p => p.ObtenerMatrizUsuarioAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GenerarTokenAsync_ConMatriz_EmiteUnClaimPorModuloConLetrasPresentes()
        {
            var usuario = new Usuario { Id = 2, Codigo = "vendedor", SuperUsuario = "N" };
            _permisoDomain.Setup(p => p.ObtenerMatrizUsuarioAsync(2)).ReturnsAsync(new List<PermisoModulo>
            {
                new("Articulos", true, true, false, false),
                new("Almacenes", false, false, false, false), // nada marcado -> no emite claim
            });

            var (token, _) = await _service.GenerarTokenAsync(usuario);

            var claims = LeerClaims(token);
            Assert.Contains(claims, c => c.Type == "perm" && c.Value == "Articulos:VA");
            Assert.DoesNotContain(claims, c => c.Type == "perm" && c.Value.StartsWith("Almacenes"));
        }

        [Fact]
        public async Task GenerarTokenAsync_SinNingunaFila_EmiteAccesoTotalATodoElCatalogo()
        {
            var usuario = new Usuario { Id = 3, Codigo = "nuevo", SuperUsuario = "N" };
            _permisoDomain.Setup(p => p.ObtenerMatrizUsuarioAsync(3)).ReturnsAsync(new List<PermisoModulo>());
            _moduloDomain.Setup(m => m.ObtenerTodoAsync()).ReturnsAsync(new List<Modulo>
            {
                new() { Codigo = "Articulos", Nombre = "Artículos", Grupo = "Inventario" },
                new() { Codigo = "Almacenes", Nombre = "Almacenes", Grupo = "Inventario" },
            }.AsQueryable());

            var (token, _) = await _service.GenerarTokenAsync(usuario);

            var claims = LeerClaims(token);
            Assert.Contains(claims, c => c.Type == "perm" && c.Value == "Articulos:VAUE");
            Assert.Contains(claims, c => c.Type == "perm" && c.Value == "Almacenes:VAUE");
        }
    }
}
```

(`using System.Linq;` y `using API.Domain.Entity.Models;` si el archivo los necesita; `Claim` es
`System.Security.Claims.Claim` — agregar `using System.Security.Claims;`.)

- [ ] **Step 2: Ejecutar la suite y verificar que falla por compilación**

```powershell
cd C:\Users\migue\source\repos\angelm0508\API
dotnet build API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apibuild\"
```

- [ ] **Step 3: Cambiar `ITokenService`**

`API.Application.Interface/ITokenService.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Application.Interface
{
    public interface ITokenService
    {
        Task<(string Token, DateTime ExpiraEn)> GenerarTokenAsync(Usuario usuario);
    }
}
```

- [ ] **Step 4: Implementar en `TokenService`**

`API.Application.Main/TokenService.cs` — reemplazar el archivo completo:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace API.Application.Main
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        private readonly IPermisoDomain _permisoDomain;
        private readonly IModuloDomain _moduloDomain;

        public TokenService(IConfiguration configuration, IPermisoDomain permisoDomain, IModuloDomain moduloDomain)
        {
            _configuration = configuration;
            _permisoDomain = permisoDomain;
            _moduloDomain = moduloDomain;
        }

        public async Task<(string Token, DateTime ExpiraEn)> GenerarTokenAsync(Usuario usuario)
        {
            var key = _configuration["Jwt:Key"];
            var issuer = _configuration["Jwt:Issuer"];
            var audience = _configuration["Jwt:Audience"];
            var expirationMinutes = int.TryParse(_configuration["Jwt:ExpirationMinutes"], out var minutos) ? minutos : 60;

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new(ClaimTypes.Name, usuario.Codigo),
                new("SuperUsuario", usuario.SuperUsuario ?? "N")
            };

            if (usuario.SuperUsuario != "S")
            {
                claims.AddRange(await ArmarClaimsPermisoAsync(usuario.Id));
            }

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key!));
            var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var expiraEn = DateTime.UtcNow.AddMinutes(expirationMinutes);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expiraEn,
                signingCredentials: credentials);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return (tokenString, expiraEn);
        }

        // Sin fila en la matriz = acceso total (decision de la spec): se emite un claim "perm"
        // con las 4 letras por cada modulo del catalogo.
        private async Task<List<Claim>> ArmarClaimsPermisoAsync(int usuarioId)
        {
            var matriz = await _permisoDomain.ObtenerMatrizUsuarioAsync(usuarioId);
            var claims = new List<Claim>();

            if (matriz.Count == 0)
            {
                var modulos = await (await _moduloDomain.ObtenerTodoAsync()).ToListAsync();
                foreach (var m in modulos)
                    claims.Add(new Claim("perm", $"{m.Codigo}:VAUE"));
                return claims;
            }

            foreach (var m in matriz)
            {
                var letras = (m.Ver ? "V" : "") + (m.Agregar ? "A" : "") + (m.Actualizar ? "U" : "") + (m.Eliminar ? "E" : "");
                if (letras.Length > 0) claims.Add(new Claim("perm", $"{m.CodigoModulo}:{letras}"));
            }
            return claims;
        }
    }
}
```

- [ ] **Step 5: Actualizar `AuthApplication.LoginAsync`**

En `API.Application.Main/AuthApplication.cs`, cambiar la línea:

```csharp
var (token, expira) = _tokenService.GenerarToken(usuario);
```

por:

```csharp
var (token, expira) = await _tokenService.GenerarTokenAsync(usuario);
```

- [ ] **Step 6: Decorar `UsuarioController`**

`API.Service.WebApi/Controllers/UsuarioController.cs` — agregar
`using API.Domain.Interface;` y `using API.Service.WebApi.Authorization;`, y un
`[RequierePermiso("Usuarios", Accion.X)]` encima de cada `[Http...]` existente:
`Obtener`/`ObtenerPorCodigo`/`ObteneContengaNombreAsync`/`ObtenerTodoAsync` → `Accion.Ver`;
`InsertarAsync` → `Accion.Agregar`; `ActualizarAsync` → `Accion.Actualizar`; `EliminarAsync` →
`Accion.Eliminar`.

- [ ] **Step 7: Ejecutar la suite completa**

```powershell
dotnet build API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apibuild\"
dotnet test API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apitest\"
```
Esperado: `0` Errores, suite completa en verde.

- [ ] **Step 8: Commit**

```bash
git add API.Application.Interface/ITokenService.cs API.Application.Main/TokenService.cs API.Application.Main/AuthApplication.cs API.Service.WebApi/Controllers/UsuarioController.cs API.Service.WebApi.Tests/Application/TokenServiceTests.cs
git commit -m "feat(permisos): TokenService emite claims perm y decora UsuarioController

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Fase 2 — API: decorar los controllers existentes

**Regla mecánica única para toda la Fase 2** (Tasks 7-10): en cada archivo de la tabla de la
tarea, agregar `using API.Domain.Interface;` y `using API.Service.WebApi.Authorization;` si el
archivo no los tiene, y colocar `[RequierePermiso("<CodigoModulo>", Accion.<Accion>)]` como línea
inmediatamente **encima** de cada atributo `[Http...]` existente (entre el `[Http...]` y el
`[RequierePermiso]` no va nada más — mismo orden en todos los archivos, para que sea visualmente
idéntico en todo el proyecto). No se toca ninguna otra línea de estos controllers. Cada tarea
incluye un ejemplo completo de un archivo ya transformado — el resto de los archivos de esa tarea
reciben exactamente la misma transformación, solo cambia qué línea de la tabla aplica a cada
método.

Ninguno de estos controllers tiene tests que fallen por este cambio (no hay infraestructura de
auth en los tests de controller existentes — usan el controller directo con Applications
mockeadas, sin pasar por el pipeline de filtros de ASP.NET) — la verificación de esta fase es
`dotnet build` limpio + la suite completa sin regresiones (el atributo nuevo no cambia ninguna
firma ni comportamiento fuera del pipeline HTTP real).

### Task 7: Decorar controllers de Ventas (Cotizacion, Pedido, Entrega, Factura + sus Detalle)

**Files:** los 8 listados en la tabla.

- [ ] **Step 1: Ejemplo completo — `CotizacionController.cs`**

Antes:
```csharp
[HttpGet("{id:int}")]
public async Task<ActionResult<Respuesta<CotizacionDTO>>> Obtener([FromRoute] int id)
```

Después:
```csharp
[HttpGet("{id:int}")]
[RequierePermiso("Cotizaciones", Accion.Ver)]
public async Task<ActionResult<Respuesta<CotizacionDTO>>> Obtener([FromRoute] int id)
```

Y así con cada una de las 5 acciones del archivo, según la tabla de abajo. Agregar al tope del
archivo:
```csharp
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
```

- [ ] **Step 2: Aplicar la misma transformación al resto de la tabla**

| Archivo | Método | Accion |
|---|---|---|
| `CotizacionController.cs` | `Obtener` | `Ver` |
| `CotizacionController.cs` | `ObtenerTodoAsync` | `Ver` |
| `CotizacionController.cs` | `InsertarAsync` | `Agregar` |
| `CotizacionController.cs` | `ActualizarAsync` | `Actualizar` |
| `CotizacionController.cs` | `EliminarAsync` | `Eliminar` |
| `CotizacionDetalleController.cs` | `Obtener` | `Ver` (módulo `Cotizaciones`) |
| `CotizacionDetalleController.cs` | `ObtenerPorCotizacion` | `Ver` |
| `CotizacionDetalleController.cs` | `ObtenerTodoAsync` | `Ver` |
| `CotizacionDetalleController.cs` | `InsertarAsync` | `Agregar` |
| `CotizacionDetalleController.cs` | `ActualizarAsync` | `Actualizar` |
| `CotizacionDetalleController.cs` | `EliminarAsync` | `Eliminar` |
| `PedidoController.cs` | `Obtener` | `Ver` (módulo `Pedidos`) |
| `PedidoController.cs` | `ObtenerTodoAsync` | `Ver` |
| `PedidoController.cs` | `InsertarAsync` | `Agregar` |
| `PedidoController.cs` | `ActualizarAsync` | `Actualizar` |
| `PedidoController.cs` | `EliminarAsync` | `Eliminar` |
| `PedidoDetalleController.cs` | `Obtener` | `Ver` (módulo `Pedidos`) |
| `PedidoDetalleController.cs` | `ObtenerPorPedido` | `Ver` |
| `PedidoDetalleController.cs` | `ObtenerTodoAsync` | `Ver` |
| `PedidoDetalleController.cs` | `InsertarAsync` | `Agregar` |
| `PedidoDetalleController.cs` | `ActualizarAsync` | `Actualizar` |
| `PedidoDetalleController.cs` | `EliminarAsync` | `Eliminar` |
| `EntregaController.cs` | `Obtener` | `Ver` (módulo `Entregas`) |
| `EntregaController.cs` | `ObtenerTodoAsync` | `Ver` |
| `EntregaController.cs` | `InsertarAsync` | `Agregar` |
| `EntregaController.cs` | `ActualizarAsync` | `Actualizar` |
| `EntregaController.cs` | `EliminarAsync` | `Eliminar` |
| `EntregaDetalleController.cs` | `Obtener` | `Ver` (módulo `Entregas`) |
| `EntregaDetalleController.cs` | `ObtenerPorEntrega` | `Ver` |
| `EntregaDetalleController.cs` | `ObtenerTodoAsync` | `Ver` |
| `EntregaDetalleController.cs` | `InsertarAsync` | `Agregar` |
| `EntregaDetalleController.cs` | `ActualizarAsync` | `Actualizar` |
| `EntregaDetalleController.cs` | `EliminarAsync` | `Eliminar` |
| `FacturaController.cs` | `Obtener` | `Ver` (módulo `Facturas`) |
| `FacturaController.cs` | `ObtenerTodoAsync` | `Ver` |
| `FacturaController.cs` | `InsertarAsync` | `Agregar` |
| `FacturaController.cs` | `ActualizarAsync` | `Actualizar` |
| `FacturaController.cs` | `EliminarAsync` | `Eliminar` |
| `FacturaDetalleController.cs` | `Obtener` | `Ver` (módulo `Facturas`) |
| `FacturaDetalleController.cs` | `ObtenerPorFactura` | `Ver` |
| `FacturaDetalleController.cs` | `ObtenerTodoAsync` | `Ver` |
| `FacturaDetalleController.cs` | `InsertarAsync` | `Agregar` |
| `FacturaDetalleController.cs` | `ActualizarAsync` | `Actualizar` |
| `FacturaDetalleController.cs` | `EliminarAsync` | `Eliminar` |

- [ ] **Step 3: Build + suite completa**

```powershell
cd C:\Users\migue\source\repos\angelm0508\API
dotnet build API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apibuild\"
dotnet test API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apitest\"
```
Esperado: `0` Errores, suite igual a la baseline de esta fase (sin cambios de comportamiento,
solo atributos nuevos).

- [ ] **Step 4: Commit**

```bash
git add API.Service.WebApi/Controllers/CotizacionController.cs API.Service.WebApi/Controllers/CotizacionDetalleController.cs API.Service.WebApi/Controllers/PedidoController.cs API.Service.WebApi/Controllers/PedidoDetalleController.cs API.Service.WebApi/Controllers/EntregaController.cs API.Service.WebApi/Controllers/EntregaDetalleController.cs API.Service.WebApi/Controllers/FacturaController.cs API.Service.WebApi/Controllers/FacturaDetalleController.cs
git commit -m "feat(permisos): decora los controllers de Ventas con RequierePermiso

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 8: Decorar controllers de Compras (PedidoCompra, EntregaCompra, FacturaCompra + sus Detalle)

**Files:** los 6 listados en la tabla. Misma transformación mecánica que Task 7 (`[RequierePermiso("<CodigoModulo>", Accion.<Accion>)]` encima de cada `[Http...]`, `using API.Domain.Interface;` + `using API.Service.WebApi.Authorization;` agregados), ejemplo:

```csharp
[HttpPost]
[RequierePermiso("PedidosCompra", Accion.Agregar)]
public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] PedidoCompraCrearDTO obj)
```

- [ ] **Step 1: Aplicar la transformación según la tabla**

| Archivo | Método | Accion |
|---|---|---|
| `PedidoCompraController.cs` | `Obtener` | `Ver` (módulo `PedidosCompra`) |
| `PedidoCompraController.cs` | `ObtenerTodoAsync` | `Ver` |
| `PedidoCompraController.cs` | `InsertarAsync` | `Agregar` |
| `PedidoCompraController.cs` | `ActualizarAsync` | `Actualizar` |
| `PedidoCompraController.cs` | `EliminarAsync` | `Eliminar` |
| `PedidoCompraDetalleController.cs` | `Obtener` | `Ver` (módulo `PedidosCompra`) |
| `PedidoCompraDetalleController.cs` | `ObtenerPorPedidoCompra` | `Ver` |
| `PedidoCompraDetalleController.cs` | `ObtenerTodoAsync` | `Ver` |
| `PedidoCompraDetalleController.cs` | `InsertarAsync` | `Agregar` |
| `PedidoCompraDetalleController.cs` | `ActualizarAsync` | `Actualizar` |
| `PedidoCompraDetalleController.cs` | `EliminarAsync` | `Eliminar` |
| `EntregaCompraController.cs` | `Obtener` | `Ver` (módulo `EntregasCompra`) |
| `EntregaCompraController.cs` | `ObtenerTodoAsync` | `Ver` |
| `EntregaCompraController.cs` | `InsertarAsync` | `Agregar` |
| `EntregaCompraController.cs` | `ActualizarAsync` | `Actualizar` |
| `EntregaCompraController.cs` | `EliminarAsync` | `Eliminar` |
| `EntregaCompraDetalleController.cs` | `Obtener` | `Ver` (módulo `EntregasCompra`) |
| `EntregaCompraDetalleController.cs` | `ObtenerPorEntregaCompra` | `Ver` |
| `EntregaCompraDetalleController.cs` | `ObtenerTodoAsync` | `Ver` |
| `EntregaCompraDetalleController.cs` | `InsertarAsync` | `Agregar` |
| `EntregaCompraDetalleController.cs` | `ActualizarAsync` | `Actualizar` |
| `EntregaCompraDetalleController.cs` | `EliminarAsync` | `Eliminar` |
| `FacturaCompraController.cs` | `Obtener` | `Ver` (módulo `FacturasCompra`) |
| `FacturaCompraController.cs` | `ObtenerTodoAsync` | `Ver` |
| `FacturaCompraController.cs` | `InsertarAsync` | `Agregar` |
| `FacturaCompraController.cs` | `ActualizarAsync` | `Actualizar` |
| `FacturaCompraController.cs` | `EliminarAsync` | `Eliminar` |
| `FacturaCompraDetalleController.cs` | `Obtener` | `Ver` (módulo `FacturasCompra`) |
| `FacturaCompraDetalleController.cs` | `ObtenerPorFacturaCompra` | `Ver` |
| `FacturaCompraDetalleController.cs` | `ObtenerTodoAsync` | `Ver` |
| `FacturaCompraDetalleController.cs` | `InsertarAsync` | `Agregar` |
| `FacturaCompraDetalleController.cs` | `ActualizarAsync` | `Actualizar` |
| `FacturaCompraDetalleController.cs` | `EliminarAsync` | `Eliminar` |

**Nota de coordinación entre planes:** si al ejecutar esta tarea `PedidoCompraController.cs` o
`EntregaCompraController.cs` ya tienen una acción `Abiertos` (agregada por el plan
`2026-09-13-documentos-base-compras-implementation.md` si avanzó mientras tanto), agregarle
también `[RequierePermiso("<SuModulo>", Accion.Ver)]` siguiendo la misma regla — no se listó
arriba porque no existía en el momento de escribir este plan.

- [ ] **Step 2: Build + suite completa**

```powershell
cd C:\Users\migue\source\repos\angelm0508\API
dotnet build API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apibuild\"
dotnet test API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apitest\"
```

- [ ] **Step 3: Commit**

```bash
git add API.Service.WebApi/Controllers/PedidoCompraController.cs API.Service.WebApi/Controllers/PedidoCompraDetalleController.cs API.Service.WebApi/Controllers/EntregaCompraController.cs API.Service.WebApi/Controllers/EntregaCompraDetalleController.cs API.Service.WebApi/Controllers/FacturaCompraController.cs API.Service.WebApi/Controllers/FacturaCompraDetalleController.cs
git commit -m "feat(permisos): decora los controllers de Compras con RequierePermiso

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 9: Decorar controllers de Inventario (13 archivos)

**Files:** los 13 listados en la tabla. Misma transformación mecánica que Tasks 7-8, ejemplo:

```csharp
[HttpPut("{codigo}")]
[RequierePermiso("Almacenes", Accion.Actualizar)]
public async Task<ActionResult<Respuesta<bool>>> Actualizar([FromRoute] string codigo, [FromBody] AlmacenActualizarDTO obj)
```

- [ ] **Step 1: Aplicar la transformación según la tabla**

| Archivo | Método | Accion |
|---|---|---|
| `ArticuloController.cs` | `Articulo` | `Ver` (módulo `Articulos`) |
| `ArticuloController.cs` | `Precio` | `Ver` |
| `ArticuloController.cs` | `ArticuloPorNombre` | `Ver` |
| `ArticuloController.cs` | `ArticulosContenganNombre` | `Ver` |
| `ArticuloController.cs` | `ArticulosContenganCodigo` | `Ver` |
| `ArticuloController.cs` | `Obtener` | `Ver` |
| `ArticuloController.cs` | `Post` | `Agregar` |
| `ArticuloController.cs` | `Update` | `Actualizar` |
| `ArticuloController.cs` | `Delete` | `Eliminar` |
| `AlmacenController.cs` | `ObtenerPorCodigo` | `Ver` (módulo `Almacenes`) |
| `AlmacenController.cs` | `ObtenerPorNombre` | `Ver` |
| `AlmacenController.cs` | `ObtenerContengaNombre` | `Ver` |
| `AlmacenController.cs` | `ObtenerContengaCodigo` | `Ver` |
| `AlmacenController.cs` | `ObtenerTodo` | `Ver` |
| `AlmacenController.cs` | `Crear` | `Agregar` |
| `AlmacenController.cs` | `Actualizar` | `Actualizar` |
| `AlmacenController.cs` | `Eliminar` | `Eliminar` |
| `FabricanteArticuloController.cs` | `Obtener` | `Ver` (módulo `Fabricantes`) |
| `FabricanteArticuloController.cs` | `ObtenerPorNombre` | `Ver` |
| `FabricanteArticuloController.cs` | `ObteneContengaNombreAsync` | `Ver` |
| `FabricanteArticuloController.cs` | `ObtenerTodoAsync` | `Ver` |
| `FabricanteArticuloController.cs` | `InsertarAsync` | `Agregar` |
| `FabricanteArticuloController.cs` | `ActualizarAsync` | `Actualizar` |
| `FabricanteArticuloController.cs` | `EliminarAsync` | `Eliminar` |
| `GrupoArticuloController.cs` | `Obtener` | `Ver` (módulo `GruposArticulo`) |
| `GrupoArticuloController.cs` | `ObtenerPorNombre` | `Ver` |
| `GrupoArticuloController.cs` | `ObteneContengaNombreAsync` | `Ver` |
| `GrupoArticuloController.cs` | `ObtenerTodoAsync` | `Ver` |
| `GrupoArticuloController.cs` | `GetAllWithPaging` | `Ver` |
| `GrupoArticuloController.cs` | `InsertarAsync` | `Agregar` |
| `GrupoArticuloController.cs` | `ActualizarAsync` | `Actualizar` |
| `GrupoArticuloController.cs` | `EliminarAsync` | `Eliminar` |
| `UnidadMedidaArticuloController.cs` | `Obtener` | `Ver` (módulo `UnidadesMedida`) |
| `UnidadMedidaArticuloController.cs` | `ObtenerPorCodigo` | `Ver` |
| `UnidadMedidaArticuloController.cs` | `ObteneContengaNombreAsync` | `Ver` |
| `UnidadMedidaArticuloController.cs` | `ObtenerTodoAsync` | `Ver` |
| `UnidadMedidaArticuloController.cs` | `InsertarAsync` | `Agregar` |
| `UnidadMedidaArticuloController.cs` | `ActualizarAsync` | `Actualizar` |
| `UnidadMedidaArticuloController.cs` | `EliminarAsync` | `Eliminar` |
| `GrupoUnidadMedidaArticuloController.cs` | `Obtener` | `Ver` (módulo `GruposUnidadMedida`) |
| `GrupoUnidadMedidaArticuloController.cs` | `ObtenerPorNombre` | `Ver` |
| `GrupoUnidadMedidaArticuloController.cs` | `ObteneContengaNombreAsync` | `Ver` |
| `GrupoUnidadMedidaArticuloController.cs` | `ObtenerTodoAsync` | `Ver` |
| `GrupoUnidadMedidaArticuloController.cs` | `InsertarAsync` | `Agregar` |
| `GrupoUnidadMedidaArticuloController.cs` | `ActualizarAsync` | `Actualizar` |
| `GrupoUnidadMedidaArticuloController.cs` | `EliminarAsync` | `Eliminar` |
| `GrupoUnidadMedidaDetArticuloController.cs` | `Obtener` | `Ver` (módulo `GruposUnidadMedida`) |
| `GrupoUnidadMedidaDetArticuloController.cs` | `ObtenerPorGrupo` | `Ver` |
| `GrupoUnidadMedidaDetArticuloController.cs` | `ObtenerTodoAsync` | `Ver` |
| `GrupoUnidadMedidaDetArticuloController.cs` | `InsertarAsync` | `Agregar` |
| `GrupoUnidadMedidaDetArticuloController.cs` | `ActualizarAsync` | `Actualizar` |
| `GrupoUnidadMedidaDetArticuloController.cs` | `EliminarAsync` | `Eliminar` |
| `ExistenciaController.cs` | `ObtenerTodo` | `Ver` (módulo `Existencias`) |
| `ExistenciaController.cs` | `Obtener` | `Ver` |
| `ExistenciaController.cs` | `ObtenerPorArticulo` | `Ver` |
| `MovimientoInventarioController.cs` | `ObtenerPorArticulo` | `Ver` (módulo `Existencias`) |
| `EntradaMercanciaController.cs` | `Obtener` | `Ver` (módulo `EntradasMercancia`) |
| `EntradaMercanciaController.cs` | `ObtenerTodoAsync` | `Ver` |
| `EntradaMercanciaController.cs` | `InsertarAsync` | `Agregar` |
| `EntradaMercanciaController.cs` | `ActualizarAsync` | `Actualizar` |
| `EntradaMercanciaController.cs` | `EliminarAsync` | `Eliminar` |
| `EntradaMercanciaDetalleController.cs` | `Obtener` | `Ver` (módulo `EntradasMercancia`) |
| `EntradaMercanciaDetalleController.cs` | `ObtenerPorEntradaMercancia` | `Ver` |
| `EntradaMercanciaDetalleController.cs` | `ObtenerTodoAsync` | `Ver` |
| `EntradaMercanciaDetalleController.cs` | `InsertarAsync` | `Agregar` |
| `EntradaMercanciaDetalleController.cs` | `ActualizarAsync` | `Actualizar` |
| `EntradaMercanciaDetalleController.cs` | `EliminarAsync` | `Eliminar` |
| `SalidaMercanciaController.cs` | `Obtener` | `Ver` (módulo `SalidasMercancia`) |
| `SalidaMercanciaController.cs` | `ObtenerTodoAsync` | `Ver` |
| `SalidaMercanciaController.cs` | `InsertarAsync` | `Agregar` |
| `SalidaMercanciaController.cs` | `ActualizarAsync` | `Actualizar` |
| `SalidaMercanciaController.cs` | `EliminarAsync` | `Eliminar` |
| `SalidaMercanciaDetalleController.cs` | `Obtener` | `Ver` (módulo `SalidasMercancia`) |
| `SalidaMercanciaDetalleController.cs` | `ObtenerPorSalidaMercancia` | `Ver` |
| `SalidaMercanciaDetalleController.cs` | `ObtenerTodoAsync` | `Ver` |
| `SalidaMercanciaDetalleController.cs` | `InsertarAsync` | `Agregar` |
| `SalidaMercanciaDetalleController.cs` | `ActualizarAsync` | `Actualizar` |
| `SalidaMercanciaDetalleController.cs` | `EliminarAsync` | `Eliminar` |

- [ ] **Step 2: Build + suite completa**

```powershell
cd C:\Users\migue\source\repos\angelm0508\API
dotnet build API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apibuild\"
dotnet test API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apitest\"
```

- [ ] **Step 3: Commit**

```bash
git add API.Service.WebApi/Controllers/ArticuloController.cs API.Service.WebApi/Controllers/AlmacenController.cs API.Service.WebApi/Controllers/FabricanteArticuloController.cs API.Service.WebApi/Controllers/GrupoArticuloController.cs API.Service.WebApi/Controllers/UnidadMedidaArticuloController.cs API.Service.WebApi/Controllers/GrupoUnidadMedidaArticuloController.cs API.Service.WebApi/Controllers/GrupoUnidadMedidaDetArticuloController.cs API.Service.WebApi/Controllers/ExistenciaController.cs API.Service.WebApi/Controllers/MovimientoInventarioController.cs API.Service.WebApi/Controllers/EntradaMercanciaController.cs API.Service.WebApi/Controllers/EntradaMercanciaDetalleController.cs API.Service.WebApi/Controllers/SalidaMercanciaController.cs API.Service.WebApi/Controllers/SalidaMercanciaDetalleController.cs
git commit -m "feat(permisos): decora los controllers de Inventario con RequierePermiso

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 10: Decorar controllers de Socios de Negocio y Documentos (6 archivos)

**Files:** los 6 listados en la tabla. Misma transformación mecánica que Tasks 7-9.

**Caso especial — `NumeracionDocumentoDetController.GenerarCodigoAsync`:** es un `[HttpPost]` pero
no persiste nada (solo calcula y devuelve el siguiente número de la serie, como preview —
"el consecutivo avanza solo al registrar el documento real", ver comentarios de los dominios de
documento). Por eso lleva `Accion.Ver`, no `Agregar`, aunque sea POST — es la única excepción a
"POST = Agregar" en todo este plan.

- [ ] **Step 1: Aplicar la transformación según la tabla**

| Archivo | Método | Accion |
|---|---|---|
| `SocioNegocioController.cs` | `ObtenerPorCodigo` | `Ver` (módulo `SociosNegocio`) |
| `SocioNegocioController.cs` | `ObtenerPorNombre` | `Ver` |
| `SocioNegocioController.cs` | `ObtenerContengaNombre` | `Ver` |
| `SocioNegocioController.cs` | `ObtenerContengaCodigo` | `Ver` |
| `SocioNegocioController.cs` | `ObtenerTodo` | `Ver` |
| `SocioNegocioController.cs` | `Crear` | `Agregar` |
| `SocioNegocioController.cs` | `Actualizar` | `Actualizar` |
| `SocioNegocioController.cs` | `Eliminar` | `Eliminar` |
| `DireccionSocioNegocioController.cs` | `ObtenerPorCodigo` | `Ver` (módulo `SociosNegocio`) |
| `DireccionSocioNegocioController.cs` | `ObtenerContengaCodigo` | `Ver` |
| `DireccionSocioNegocioController.cs` | `ObtenerTodo` | `Ver` |
| `DireccionSocioNegocioController.cs` | `Crear` | `Agregar` |
| `DireccionSocioNegocioController.cs` | `Actualizar` | `Actualizar` |
| `DireccionSocioNegocioController.cs` | `Eliminar` | `Eliminar` |
| `GrupoSnController.cs` | `Obtener` | `Ver` (módulo `GruposSn`) |
| `GrupoSnController.cs` | `ObtenerPorNombre` | `Ver` |
| `GrupoSnController.cs` | `ObteneContengaNombreAsync` | `Ver` |
| `GrupoSnController.cs` | `ObtenerTodoAsync` | `Ver` |
| `GrupoSnController.cs` | `InsertarAsync` | `Agregar` |
| `GrupoSnController.cs` | `ActualizarAsync` | `Actualizar` |
| `GrupoSnController.cs` | `EliminarAsync` | `Eliminar` |
| `ListadoPrecioController.cs` | `Obtener` | `Ver` (módulo `ListadosPrecio`) |
| `ListadoPrecioController.cs` | `ObtenerPorNombre` | `Ver` |
| `ListadoPrecioController.cs` | `ObteneContengaNombreAsync` | `Ver` |
| `ListadoPrecioController.cs` | `ObtenerTodoAsync` | `Ver` |
| `ListadoPrecioController.cs` | `InsertarAsync` | `Agregar` |
| `ListadoPrecioController.cs` | `ActualizarAsync` | `Actualizar` |
| `ListadoPrecioController.cs` | `EliminarAsync` | `Eliminar` |
| `NumeracionDocumentoController.cs` | `ObtenerPorCodigo` | `Ver` (módulo `NumeracionDocumento`) |
| `NumeracionDocumentoController.cs` | `ObtenerContengaCodigo` | `Ver` |
| `NumeracionDocumentoController.cs` | `ObtenerTodo` | `Ver` |
| `NumeracionDocumentoController.cs` | `Crear` | `Agregar` |
| `NumeracionDocumentoController.cs` | `Actualizar` | `Actualizar` |
| `NumeracionDocumentoController.cs` | `Eliminar` | `Eliminar` |
| `NumeracionDocumentoDetController.cs` | `Obtener` | `Ver` (módulo `NumeracionDocumento`) |
| `NumeracionDocumentoDetController.cs` | `ObtenerPorDocumento` | `Ver` |
| `NumeracionDocumentoDetController.cs` | `ObtenerTodoAsync` | `Ver` |
| `NumeracionDocumentoDetController.cs` | `InsertarAsync` | `Agregar` |
| `NumeracionDocumentoDetController.cs` | `ActualizarAsync` | `Actualizar` |
| `NumeracionDocumentoDetController.cs` | `EliminarAsync` | `Eliminar` |
| `NumeracionDocumentoDetController.cs` | `GenerarCodigoAsync` | `Ver` (caso especial, ver arriba) |

- [ ] **Step 2: Build + suite completa**

```powershell
cd C:\Users\migue\source\repos\angelm0508\API
dotnet build API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apibuild\"
dotnet test API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apitest\"
```
Esperado: `0` Errores, suite completa igual a la baseline (Fase 2 no agrega tests, solo
atributos). Este es el cierre de la Fase 2 — los 39 controllers de la API (menos `AuthController`
y los 6 catálogos de referencia excluidos: `Departamento`, `Impuesto`, `Moneda`, `Municipio`,
`Pais`, y `UsuarioController` ya decorado en Task 6) quedan protegidos.

- [ ] **Step 3: Commit**

```bash
git add API.Service.WebApi/Controllers/SocioNegocioController.cs API.Service.WebApi/Controllers/DireccionSocioNegocioController.cs API.Service.WebApi/Controllers/GrupoSnController.cs API.Service.WebApi/Controllers/ListadoPrecioController.cs API.Service.WebApi/Controllers/NumeracionDocumentoController.cs API.Service.WebApi/Controllers/NumeracionDocumentoDetController.cs
git commit -m "feat(permisos): decora los controllers de Socios de Negocio y Documentos con RequierePermiso

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Fase 3 — Web: plomería de permisos

### Task 11: Copiar claims a la cookie, `ClaimsPrincipalExtensions`, `RequierePermisoAttribute` (Web), página de acceso denegado

**Files:**
- Create: `Web.UI/Authorization/Accion.cs`
- Create: `Web.UI/Authorization/ClaimsPrincipalExtensions.cs`
- Create: `Web.UI/Authorization/RequierePermisoAttribute.cs`
- Modify: `Web.UI/Controllers/CuentaController.cs`
- Create: `Web.UI/Views/Cuenta/AccesoDenegado.cshtml`
- Modify: `Web.UI/Program.cs`

**Interfaces:**
- Produces: `enum Accion { Ver, Agregar, Actualizar, Eliminar }` (Web, espejo del de la API);
  `ClaimsPrincipal.TienePermiso(string codigoModulo, Accion accion) : bool`;
  `[RequierePermiso(string codigoModulo, Accion accion)]` para controllers MVC.

No hay tests automatizados de Web en este repo (confirmado por el resto de los planes del
proyecto) — la verificación de esta fase es `dotnet build` limpio + checklist manual al final
(Task 21).

- [ ] **Step 1: Crear `Accion` y `ClaimsPrincipalExtensions`**

`Web.UI/Authorization/Accion.cs`:

```csharp
namespace Web.UI.Authorization
{
    public enum Accion { Ver, Agregar, Actualizar, Eliminar }
}
```

`Web.UI/Authorization/ClaimsPrincipalExtensions.cs`:

```csharp
using System.Security.Claims;

namespace Web.UI.Authorization
{
    public static class ClaimsPrincipalExtensions
    {
        public static bool TienePermiso(this ClaimsPrincipal user, string codigoModulo, Accion accion)
        {
            if (user.FindFirst("SuperUsuario")?.Value == "S") return true;

            var letra = accion switch
            {
                Accion.Ver => 'V',
                Accion.Agregar => 'A',
                Accion.Actualizar => 'U',
                Accion.Eliminar => 'E',
                _ => ' '
            };

            return user.FindAll("perm")
                .Select(c => c.Value.Split(':'))
                .Where(p => p.Length == 2 && p[0] == codigoModulo)
                .Any(p => p[1].Contains(letra));
        }
    }
}
```

- [ ] **Step 2: Crear `RequierePermisoAttribute` (Web)**

`Web.UI/Authorization/RequierePermisoAttribute.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Web.UI.Authorization
{
    [AttributeUsage(AttributeTargets.Method)]
    public class RequierePermisoAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string _codigoModulo;
        private readonly Accion _accion;

        public RequierePermisoAttribute(string codigoModulo, Accion accion)
        {
            _codigoModulo = codigoModulo;
            _accion = accion;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (!context.HttpContext.User.TienePermiso(_codigoModulo, _accion))
            {
                context.Result = new RedirectToActionResult("AccesoDenegado", "Cuenta", null);
            }
        }
    }
}
```

- [ ] **Step 3: Copiar los claims `SuperUsuario`/`perm` del JWT a la cookie en `CuentaController.Login`**

En `Web.UI/Controllers/CuentaController.cs`, agregar `using System.IdentityModel.Tokens.Jwt;` al
tope, y reemplazar el bloque donde arma `claims`:

```csharp
var claims = new List<Claim>
{
    new(ClaimTypes.Name, respuesta.Dato.UsuarioNombre ?? modelo.Usuario),
    new(AuthConstants.ClaimJwtToken, respuesta.Dato.Token)
};
```

por:

```csharp
var claims = new List<Claim>
{
    new(ClaimTypes.Name, respuesta.Dato.UsuarioNombre ?? modelo.Usuario),
    new(AuthConstants.ClaimJwtToken, respuesta.Dato.Token)
};

// El JWT ya fue validado/firmado por la API al emitirlo -- acá solo se decodifica para copiar
// sus claims de autorizacion (SuperUsuario/perm) a la cookie de sesion de la Web, que hoy no
// llevaba ninguno de los dos.
var jwt = new JwtSecurityTokenHandler().ReadJwtToken(respuesta.Dato.Token);
claims.AddRange(jwt.Claims.Where(c => c.Type == "SuperUsuario" || c.Type == "perm"));
```

- [ ] **Step 4: Agregar la acción `AccesoDenegado`**

En `Web.UI/Controllers/CuentaController.cs`, agregar (junto a `Login`/`Logout`):

```csharp
[HttpGet]
public IActionResult AccesoDenegado()
{
    return View();
}
```

`Web.UI/Views/Cuenta/AccesoDenegado.cshtml`:

```html
@{
    ViewData["Title"] = "Acceso denegado";
}

<div class="d-flex flex-column align-items-center justify-content-center text-center py-5">
    <i class="fa-solid fa-lock fa-3x text-danger mb-3"></i>
    <h3>Acceso denegado</h3>
    <p class="text-muted">No tenés permiso para ver esta sección. Si creés que es un error, hablá con un administrador.</p>
    <a asp-controller="Home" asp-action="Index" class="btn btn-primary mt-2">Volver al inicio</a>
</div>
```

- [ ] **Step 5: Cambiar `AccessDeniedPath` en `Program.cs`**

En `Web.UI/Program.cs`, cambiar:

```csharp
options.AccessDeniedPath = "/Cuenta/Login";
```

por:

```csharp
options.AccessDeniedPath = "/Cuenta/AccesoDenegado";
```

- [ ] **Step 6: Build**

```powershell
cd C:\Users\migue\source\repos\angelm0508\Web
dotnet build Web.slnx -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\webbuild\"
```
Esperado: `0` Errores.

- [ ] **Step 7: Commit**

```bash
git add Web.UI/Authorization/Accion.cs Web.UI/Authorization/ClaimsPrincipalExtensions.cs Web.UI/Authorization/RequierePermisoAttribute.cs Web.UI/Controllers/CuentaController.cs Web.UI/Views/Cuenta/AccesoDenegado.cshtml Web.UI/Program.cs
git commit -m "feat(permisos): copia claims de permisos a la cookie, filtro y pagina de acceso denegado

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 12: `Web.ApiClient` — DTOs y clientes tipados nuevos

**Files:**
- Create: `Web.ApiClient/Dtos/Permiso/ModuloDTO.cs`
- Create: `Web.ApiClient/Dtos/Permiso/PermisoModuloDTO.cs`
- Create: `Web.ApiClient/Dtos/Permiso/PlantillaDTO.cs`
- Create: `Web.ApiClient/Dtos/Permiso/PlantillaCrearDTO.cs`
- Create: `Web.ApiClient/Dtos/Permiso/PlantillaActualizarDTO.cs`
- Create: `Web.ApiClient/Dtos/Usuario/UsuarioDTO.cs`
- Create: `Web.ApiClient/Clientes/IModuloApiClient.cs` + `ModuloApiClient.cs`
- Create: `Web.ApiClient/Clientes/IUsuarioPermisoApiClient.cs` + `UsuarioPermisoApiClient.cs`
- Create: `Web.ApiClient/Clientes/IPlantillaApiClient.cs` + `PlantillaApiClient.cs`
- Create: `Web.ApiClient/Clientes/IUsuarioApiClient.cs` + `UsuarioApiClient.cs`
- Modify: `Web.UI/Program.cs` (4 registros `AddHttpClient`)

**Interfaces:**
- Produces: `IModuloApiClient.ObtenerTodoAsync()`; `IUsuarioPermisoApiClient.ObtenerAsync(int)`/
  `.GuardarAsync(int, List<PermisoModuloDTO>)`/`.AplicarPlantillaAsync(int, int)`;
  `IPlantillaApiClient` con CRUD + `.ObtenerPermisosAsync(int)`/`.GuardarPermisosAsync(int, List<PermisoModuloDTO>)`;
  `IUsuarioApiClient.ObtenerTodoAsync()`/`.ObtenerAsync(int)`.

- [ ] **Step 1: Crear los DTOs**

`Web.ApiClient/Dtos/Permiso/ModuloDTO.cs`:

```csharp
namespace Web.ApiClient.Dtos.Permiso
{
    public class ModuloDTO
    {
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        public string Grupo { get; set; } = null!;
    }
}
```

`Web.ApiClient/Dtos/Permiso/PermisoModuloDTO.cs`:

```csharp
namespace Web.ApiClient.Dtos.Permiso
{
    public class PermisoModuloDTO
    {
        public string CodigoModulo { get; set; } = null!;
        public bool Ver { get; set; }
        public bool Agregar { get; set; }
        public bool Actualizar { get; set; }
        public bool Eliminar { get; set; }
    }
}
```

`Web.ApiClient/Dtos/Permiso/PlantillaDTO.cs`:

```csharp
namespace Web.ApiClient.Dtos.Permiso
{
    public class PlantillaDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
    }
}
```

`Web.ApiClient/Dtos/Permiso/PlantillaCrearDTO.cs` y `PlantillaActualizarDTO.cs` (idénticos, un
solo campo):

```csharp
namespace Web.ApiClient.Dtos.Permiso
{
    public class PlantillaCrearDTO
    {
        public string Nombre { get; set; } = null!;
    }
}
```

```csharp
namespace Web.ApiClient.Dtos.Permiso
{
    public class PlantillaActualizarDTO
    {
        public string Nombre { get; set; } = null!;
    }
}
```

`Web.ApiClient/Dtos/Usuario/UsuarioDTO.cs` (subconjunto de solo lectura — sin `Password` ni
`UltimaContra`, que no hacen falta para listar usuarios y no conviene que viajen al cliente):

```csharp
namespace Web.ApiClient.Dtos.Usuario
{
    public class UsuarioDTO
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = null!;
        public string? Nombre { get; set; }
        public string? SuperUsuario { get; set; }
        public string? Email { get; set; }
        public string? Bloqueado { get; set; }
    }
}
```

- [ ] **Step 2: Crear `IModuloApiClient` / `ModuloApiClient`**

```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.Permiso;

namespace Web.ApiClient.Clientes
{
    public interface IModuloApiClient
    {
        Task<Respuesta<IEnumerable<ModuloDTO>>> ObtenerTodoAsync();
    }
}
```

```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.Permiso;

namespace Web.ApiClient.Clientes
{
    public class ModuloApiClient : ApiClientBase, IModuloApiClient
    {
        private const string Recurso = "api/Modulo";

        public ModuloApiClient(HttpClient http) : base(http) { }

        public Task<Respuesta<IEnumerable<ModuloDTO>>> ObtenerTodoAsync() =>
            GetAsync<IEnumerable<ModuloDTO>>(Recurso);
    }
}
```

- [ ] **Step 3: Crear `IUsuarioPermisoApiClient` / `UsuarioPermisoApiClient`**

```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.Permiso;

namespace Web.ApiClient.Clientes
{
    public interface IUsuarioPermisoApiClient
    {
        Task<Respuesta<IEnumerable<PermisoModuloDTO>>> ObtenerAsync(int usuarioId);
        Task<Respuesta<bool>> GuardarAsync(int usuarioId, List<PermisoModuloDTO> matriz);
        Task<Respuesta<bool>> AplicarPlantillaAsync(int usuarioId, int plantillaId);
    }
}
```

```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.Permiso;

namespace Web.ApiClient.Clientes
{
    public class UsuarioPermisoApiClient : ApiClientBase, IUsuarioPermisoApiClient
    {
        private const string Recurso = "api/UsuarioPermiso";

        public UsuarioPermisoApiClient(HttpClient http) : base(http) { }

        public Task<Respuesta<IEnumerable<PermisoModuloDTO>>> ObtenerAsync(int usuarioId) =>
            GetAsync<IEnumerable<PermisoModuloDTO>>($"{Recurso}/{usuarioId}");

        public Task<Respuesta<bool>> GuardarAsync(int usuarioId, List<PermisoModuloDTO> matriz) =>
            PutAsync<bool>($"{Recurso}/{usuarioId}", matriz);

        public Task<Respuesta<bool>> AplicarPlantillaAsync(int usuarioId, int plantillaId) =>
            PostAsync<bool>($"{Recurso}/{usuarioId}/AplicarPlantilla/{plantillaId}", new { });
    }
}
```

- [ ] **Step 4: Crear `IPlantillaApiClient` / `PlantillaApiClient`**

```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.Permiso;

namespace Web.ApiClient.Clientes
{
    public interface IPlantillaApiClient
    {
        Task<Respuesta<IEnumerable<PlantillaDTO>>> ObtenerTodoAsync();
        Task<Respuesta<PlantillaDTO>> ObtenerAsync(int id);
        Task<Respuesta<int>> InsertarAsync(PlantillaCrearDTO dto);
        Task<Respuesta<bool>> ActualizarAsync(int id, PlantillaActualizarDTO dto);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<IEnumerable<PermisoModuloDTO>>> ObtenerPermisosAsync(int id);
        Task<Respuesta<bool>> GuardarPermisosAsync(int id, List<PermisoModuloDTO> matriz);
    }
}
```

```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.Permiso;

namespace Web.ApiClient.Clientes
{
    public class PlantillaApiClient : ApiClientBase, IPlantillaApiClient
    {
        private const string Recurso = "api/Plantilla";

        public PlantillaApiClient(HttpClient http) : base(http) { }

        public Task<Respuesta<IEnumerable<PlantillaDTO>>> ObtenerTodoAsync() =>
            GetAsync<IEnumerable<PlantillaDTO>>(Recurso);

        public Task<Respuesta<PlantillaDTO>> ObtenerAsync(int id) =>
            GetAsync<PlantillaDTO>($"{Recurso}/{id}");

        public Task<Respuesta<int>> InsertarAsync(PlantillaCrearDTO dto) =>
            PostAsync<int>(Recurso, dto);

        public Task<Respuesta<bool>> ActualizarAsync(int id, PlantillaActualizarDTO dto) =>
            PutAsync<bool>($"{Recurso}/{id}", dto);

        public Task<Respuesta<bool>> EliminarAsync(int id) =>
            DeleteAsync<bool>($"{Recurso}/{id}");

        public Task<Respuesta<IEnumerable<PermisoModuloDTO>>> ObtenerPermisosAsync(int id) =>
            GetAsync<IEnumerable<PermisoModuloDTO>>($"{Recurso}/{id}/Permisos");

        public Task<Respuesta<bool>> GuardarPermisosAsync(int id, List<PermisoModuloDTO> matriz) =>
            PutAsync<bool>($"{Recurso}/{id}/Permisos", matriz);
    }
}
```

- [ ] **Step 5: Crear `IUsuarioApiClient` / `UsuarioApiClient`**

```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.Usuario;

namespace Web.ApiClient.Clientes
{
    public interface IUsuarioApiClient
    {
        Task<Respuesta<IEnumerable<UsuarioDTO>>> ObtenerTodoAsync();
        Task<Respuesta<UsuarioDTO>> ObtenerAsync(int id);
    }
}
```

```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.Usuario;

namespace Web.ApiClient.Clientes
{
    public class UsuarioApiClient : ApiClientBase, IUsuarioApiClient
    {
        private const string Recurso = "api/Usuario";

        public UsuarioApiClient(HttpClient http) : base(http) { }

        public Task<Respuesta<IEnumerable<UsuarioDTO>>> ObtenerTodoAsync() =>
            GetAsync<IEnumerable<UsuarioDTO>>(Recurso);

        public Task<Respuesta<UsuarioDTO>> ObtenerAsync(int id) =>
            GetAsync<UsuarioDTO>($"{Recurso}/{id}");
    }
}
```

- [ ] **Step 6: Registrar los 4 clientes en `Program.cs`**

Agregar junto a los demás (mismo patrón, `apiBaseUrl` y `JwtAuthorizationHandler` ya están
definidos más arriba en el archivo):

```csharp
builder.Services.AddHttpClient<IModuloApiClient, ModuloApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthorizationHandler>();
builder.Services.AddHttpClient<IUsuarioPermisoApiClient, UsuarioPermisoApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthorizationHandler>();
builder.Services.AddHttpClient<IPlantillaApiClient, PlantillaApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthorizationHandler>();
builder.Services.AddHttpClient<IUsuarioApiClient, UsuarioApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthorizationHandler>();
```

- [ ] **Step 7: Build**

```powershell
cd C:\Users\migue\source\repos\angelm0508\Web
dotnet build Web.slnx -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\webbuild\"
```

- [ ] **Step 8: Commit**

```bash
git add Web.ApiClient/Dtos/Permiso/ Web.ApiClient/Dtos/Usuario/ Web.ApiClient/Clientes/IModuloApiClient.cs Web.ApiClient/Clientes/ModuloApiClient.cs Web.ApiClient/Clientes/IUsuarioPermisoApiClient.cs Web.ApiClient/Clientes/UsuarioPermisoApiClient.cs Web.ApiClient/Clientes/IPlantillaApiClient.cs Web.ApiClient/Clientes/PlantillaApiClient.cs Web.ApiClient/Clientes/IUsuarioApiClient.cs Web.ApiClient/Clientes/UsuarioApiClient.cs Web.UI/Program.cs
git commit -m "feat(permisos): clientes tipados de Modulo, UsuarioPermiso, Plantilla y Usuario

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Fase 4 — Web: pantallas nuevas

### Task 13: `UsuariosController` + lista de usuarios

**Files:**
- Create: `Web.UI/Controllers/UsuariosController.cs`
- Create: `Web.UI/Views/Usuarios/Index.cshtml`
- Create: `Web.UI/wwwroot/js/usuarios.js`

**Interfaces:**
- Consumes: `IUsuarioApiClient` (Task 12).
- Produces: `GET /Usuarios` (página), `GET /Usuarios/ObtenerTodos` (Json), rutas para Task 14
  (`/Usuarios/Permisos?usuarioId=`).

Pantalla de solo lectura (sin alta/edición de usuario, fuera de alcance por spec §Contexto) — un
listado con un botón "Permisos" por fila. Esta pantalla y `PlantillasController` (Task 15) son los
únicos controllers nuevos protegidos con `[RequierePermiso("Usuarios", ...)]` — ver Task 16 para
por qué en la práctica solo `SuperUsuario` llega a verlos en el menú.

- [ ] **Step 1: `UsuariosController`**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.ApiClient.Clientes;
using Web.UI.Authorization;

namespace Web.UI.Controllers
{
    [Authorize]
    public class UsuariosController : Controller
    {
        private readonly IUsuarioApiClient _usuarios;

        public UsuariosController(IUsuarioApiClient usuarios)
        {
            _usuarios = usuarios;
        }

        [RequierePermiso("Usuarios", Accion.Ver)]
        public IActionResult Index() => View();

        [HttpGet]
        [RequierePermiso("Usuarios", Accion.Ver)]
        public async Task<IActionResult> ObtenerTodos()
        {
            var respuesta = await _usuarios.ObtenerTodoAsync();
            return Json(respuesta);
        }
    }
}
```

- [ ] **Step 2: `Views/Usuarios/Index.cshtml`**

```html
@{
    ViewData["Title"] = "Usuarios";
}

<div class="d-flex justify-content-between align-items-center mb-3">
    <h3 class="mb-0">Usuarios</h3>
</div>

<div class="card card-modulo">
    <div class="card-body">
        <div class="table-responsive">
            <table id="tblUsuarios" class="table table-hover align-middle w-100">
                <thead>
                    <tr>
                        <th>Código</th>
                        <th>Nombre</th>
                        <th>Superusuario</th>
                        <th>Estado</th>
                        <th class="text-end">Acciones</th>
                    </tr>
                </thead>
                <tbody></tbody>
            </table>
        </div>
    </div>
</div>

@section Scripts {
    <script src="~/js/usuarios.js" asp-append-version="true"></script>
}
```

- [ ] **Step 3: `wwwroot/js/usuarios.js`**

```javascript
$(function () {
    $('#tblUsuarios').DataTable({
        ajax: { url: '/Usuarios/ObtenerTodos', dataSrc: App.dataSrcTabla },
        columns: [
            { data: 'codigo' },
            { data: 'nombre', render: d => d || '' },
            {
                data: 'superUsuario',
                render: d => d === 'S' ? '<span class="badge text-bg-primary">Sí</span>' : ''
            },
            {
                data: 'bloqueado',
                render: d => d === 'S' ? '<span class="badge text-bg-danger">Bloqueado</span>' : '<span class="badge text-bg-success">Activo</span>'
            },
            {
                data: 'id', orderable: false, className: 'text-end',
                render: id => `<a class="btn btn-sm btn-outline-primary" href="/Usuarios/Permisos?usuarioId=${id}"><i class="fa-solid fa-key"></i> Permisos</a>`
            }
        ],
        language: App.datatableEsEs
    });
});
```

- [ ] **Step 4: Build**

```powershell
cd C:\Users\migue\source\repos\angelm0508\Web
dotnet build Web.slnx -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\webbuild\"
```

- [ ] **Step 5: Commit**

```bash
git add Web.UI/Controllers/UsuariosController.cs Web.UI/Views/Usuarios/Index.cshtml Web.UI/wwwroot/js/usuarios.js
git commit -m "feat(permisos): pantalla de lista de usuarios (solo lectura)

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 14: Grilla de permisos compartida + pantalla de permisos de usuario (con "Aplicar plantilla")

**Files:**
- Create: `Web.UI/wwwroot/js/permisos.js`
- Create: `Web.UI/Views/Usuarios/Permisos.cshtml`
- Modify: `Web.UI/Controllers/UsuariosController.cs`

**Interfaces:**
- Consumes: `IModuloApiClient`, `IUsuarioPermisoApiClient`, `IPlantillaApiClient` (Task 12).
- Produces: `pintarGrillaPermisos(modulos, matriz) : string` (HTML), `leerGrillaPermisos() : Array`
  (JS, usados también por Task 15); rutas `GET /Usuarios/Permisos?usuarioId=`,
  `GET /Usuarios/ObtenerModulos`, `GET /Usuarios/ObtenerPermisos?usuarioId=`,
  `POST /Usuarios/GuardarPermisos`, `GET /Usuarios/ObtenerPlantillas`,
  `POST /Usuarios/AplicarPlantilla`.

- [ ] **Step 1: `wwwroot/js/permisos.js`** (compartido, sin IIFE propio — se incluye junto al
  script de cada página que lo usa, igual que `site.js`)

```javascript
/** Agrupa el catalogo de modulos por su Grupo, preservando el orden en que llegaron. */
function agruparModulosPorGrupo(modulos) {
    const grupos = [];
    const indice = {};
    modulos.forEach(m => {
        const grupo = m.grupo ?? m.Grupo;
        if (!(grupo in indice)) {
            indice[grupo] = grupos.length;
            grupos.push({ nombre: grupo, modulos: [] });
        }
        grupos[indice[grupo]].modulos.push(m);
    });
    return grupos;
}

/** Arma el HTML de la grilla modulo x 4 acciones, marcando los checkboxes segun la matriz actual. */
function pintarGrillaPermisos(modulos, matriz) {
    const porCodigo = {};
    (matriz ?? []).forEach(p => { porCodigo[p.codigoModulo ?? p.CodigoModulo] = p; });

    const filas = agruparModulosPorGrupo(modulos).map(grupo => {
        const filasGrupo = grupo.modulos.map(m => {
            const codigo = m.codigo ?? m.Codigo;
            const nombre = m.nombre ?? m.Nombre;
            const p = porCodigo[codigo] ?? {};
            const chk = (campo) => (p[campo] ?? p[campo.charAt(0).toUpperCase() + campo.slice(1)]) ? 'checked' : '';
            return `
                <tr data-codigo-modulo="${codigo}">
                    <td>${nombre}</td>
                    <td class="text-center"><input type="checkbox" class="chkVer" ${chk('ver')}></td>
                    <td class="text-center"><input type="checkbox" class="chkAgregar" ${chk('agregar')}></td>
                    <td class="text-center"><input type="checkbox" class="chkActualizar" ${chk('actualizar')}></td>
                    <td class="text-center"><input type="checkbox" class="chkEliminar" ${chk('eliminar')}></td>
                </tr>`;
        }).join('');
        return `<tr class="table-light"><td colspan="5"><strong>${grupo.nombre}</strong></td></tr>${filasGrupo}`;
    }).join('');

    return filas;
}

/** Lee el estado actual de los checkboxes de #tblGrillaPermisos y arma la matriz a guardar. */
function leerGrillaPermisos() {
    const matriz = [];
    $('#tblGrillaPermisos tbody tr[data-codigo-modulo]').each(function () {
        const $fila = $(this);
        matriz.push({
            CodigoModulo: $fila.data('codigo-modulo'),
            Ver: $fila.find('.chkVer').is(':checked'),
            Agregar: $fila.find('.chkAgregar').is(':checked'),
            Actualizar: $fila.find('.chkActualizar').is(':checked'),
            Eliminar: $fila.find('.chkEliminar').is(':checked')
        });
    });
    return matriz;
}
```

- [ ] **Step 2: `Views/Usuarios/Permisos.cshtml`**

```html
@{
    ViewData["Title"] = "Permisos de usuario";
    var usuarioId = Context.Request.Query["usuarioId"];
}

<div class="d-flex justify-content-between align-items-center mb-3">
    <h3 class="mb-0">Permisos de usuario</h3>
    <a asp-controller="Usuarios" asp-action="Index" class="btn btn-sm btn-outline-secondary">
        <i class="fa-solid fa-arrow-left me-1"></i>Volver
    </a>
</div>

<div class="card card-modulo">
    <div class="card-body">
        <div class="row g-2 align-items-end mb-3">
            <div class="col-md-5">
                <label class="form-label">Aplicar plantilla</label>
                <select id="selectPlantilla" class="form-select">
                    <option value="">-- Seleccione --</option>
                </select>
            </div>
            <div class="col-md-3">
                <button type="button" class="btn btn-outline-primary" id="btnAplicarPlantilla">
                    <i class="fa-solid fa-copy me-1"></i>Aplicar
                </button>
            </div>
        </div>

        <div class="table-responsive">
            <table id="tblGrillaPermisos" class="table table-sm table-hover align-middle">
                <thead>
                    <tr>
                        <th>Módulo</th>
                        <th class="text-center">Ver</th>
                        <th class="text-center">Agregar</th>
                        <th class="text-center">Actualizar</th>
                        <th class="text-center">Eliminar</th>
                    </tr>
                </thead>
                <tbody></tbody>
            </table>
        </div>

        <div class="text-end mt-3">
            <button type="button" class="btn btn-primary" id="btnGuardarPermisos">
                <i class="fa-solid fa-floppy-disk me-1"></i>Guardar
            </button>
        </div>
    </div>
</div>

<input type="hidden" id="usuarioIdActual" value="@usuarioId" />

@section Scripts {
    <script src="~/js/permisos.js" asp-append-version="true"></script>
    <script>
        $(function () {
            const usuarioId = $('#usuarioIdActual').val();

            async function cargar() {
                const [modulosResp, permisosResp, plantillasResp] = await Promise.all([
                    $.get('/Usuarios/ObtenerModulos'),
                    $.get('/Usuarios/ObtenerPermisos', { usuarioId }),
                    $.get('/Usuarios/ObtenerPlantillas')
                ]);

                const modulos = (modulosResp.resultado && modulosResp.dato) ? modulosResp.dato : [];
                const matriz = (permisosResp.resultado && permisosResp.dato) ? permisosResp.dato : [];
                $('#tblGrillaPermisos tbody').html(pintarGrillaPermisos(modulos, matriz));

                const plantillas = (plantillasResp.resultado && plantillasResp.dato) ? plantillasResp.dato : [];
                const $select = $('#selectPlantilla');
                plantillas.forEach(p => $select.append(`<option value="${p.id ?? p.Id}">${p.nombre ?? p.Nombre}</option>`));
            }

            $(document).on('click', '#btnGuardarPermisos', async function () {
                const matriz = leerGrillaPermisos();
                const respuesta = await App.enviarJson('/Usuarios/GuardarPermisos', 'POST', { UsuarioId: Number(usuarioId), Matriz: matriz });
                if (!respuesta.resultado) {
                    App.mostrarError(respuesta.mensaje);
                    return;
                }
                App.mostrarExito('Permisos guardados correctamente.');
            });

            $(document).on('click', '#btnAplicarPlantilla', async function () {
                const plantillaId = $('#selectPlantilla').val();
                if (!plantillaId) {
                    App.mostrarError('Selecciona una plantilla.');
                    return;
                }
                const confirmado = await App.confirmarEliminar('Se reemplazarán los permisos actuales de este usuario por los de la plantilla elegida.');
                if (!confirmado) return;

                const respuesta = await App.enviarJson('/Usuarios/AplicarPlantilla', 'POST', { UsuarioId: Number(usuarioId), PlantillaId: Number(plantillaId) });
                if (!respuesta.resultado) {
                    App.mostrarError(respuesta.mensaje);
                    return;
                }
                App.mostrarExito('Plantilla aplicada correctamente.');
                cargar();
            });

            cargar();
        });
    </script>
}
```

(`App.confirmarEliminar` ya existe como helper genérico de confirmación en `site.js` — se reusa
acá aunque el texto no sea de "eliminar", el helper solo muestra un `Swal.fire` de confirmación
con el mensaje que se le pasa.)

- [ ] **Step 3: Ampliar `UsuariosController` con los proxies**

Agregar al constructor `IModuloApiClient`, `IUsuarioPermisoApiClient` e `IPlantillaApiClient`, y
las acciones:

```csharp
private readonly IModuloApiClient _modulos;
private readonly IUsuarioPermisoApiClient _usuarioPermisos;
private readonly IPlantillaApiClient _plantillas;

public UsuariosController(IUsuarioApiClient usuarios, IModuloApiClient modulos, IUsuarioPermisoApiClient usuarioPermisos, IPlantillaApiClient plantillas)
{
    _usuarios = usuarios;
    _modulos = modulos;
    _usuarioPermisos = usuarioPermisos;
    _plantillas = plantillas;
}

[RequierePermiso("Usuarios", Accion.Ver)]
public IActionResult Permisos(int usuarioId) => View();

[HttpGet]
[RequierePermiso("Usuarios", Accion.Ver)]
public async Task<IActionResult> ObtenerModulos() => Json(await _modulos.ObtenerTodoAsync());

[HttpGet]
[RequierePermiso("Usuarios", Accion.Ver)]
public async Task<IActionResult> ObtenerPermisos(int usuarioId) => Json(await _usuarioPermisos.ObtenerAsync(usuarioId));

[HttpGet]
[RequierePermiso("Usuarios", Accion.Ver)]
public async Task<IActionResult> ObtenerPlantillas() => Json(await _plantillas.ObtenerTodoAsync());

public class GuardarPermisosRequest
{
    public int UsuarioId { get; set; }
    public List<Web.ApiClient.Dtos.Permiso.PermisoModuloDTO> Matriz { get; set; } = new();
}

[HttpPost]
[RequierePermiso("Usuarios", Accion.Actualizar)]
public async Task<IActionResult> GuardarPermisos([FromBody] GuardarPermisosRequest body) =>
    Json(await _usuarioPermisos.GuardarAsync(body.UsuarioId, body.Matriz));

public class AplicarPlantillaRequest
{
    public int UsuarioId { get; set; }
    public int PlantillaId { get; set; }
}

[HttpPost]
[RequierePermiso("Usuarios", Accion.Actualizar)]
public async Task<IActionResult> AplicarPlantilla([FromBody] AplicarPlantillaRequest body) =>
    Json(await _usuarioPermisos.AplicarPlantillaAsync(body.UsuarioId, body.PlantillaId));
```

Agregar `using System.Collections.Generic;` si el archivo no lo tiene (debería, por `IEnumerable`
en otros usos del proyecto).

- [ ] **Step 4: Build**

```powershell
cd C:\Users\migue\source\repos\angelm0508\Web
dotnet build Web.slnx -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\webbuild\"
```

- [ ] **Step 5: Commit**

```bash
git add Web.UI/wwwroot/js/permisos.js Web.UI/Views/Usuarios/Permisos.cshtml Web.UI/Controllers/UsuariosController.cs
git commit -m "feat(permisos): grilla de permisos de usuario con aplicar plantilla

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 15: `PlantillasController` — CRUD y permisos de plantilla

**Files:**
- Create: `Web.UI/Controllers/PlantillasController.cs`
- Create: `Web.UI/Views/Plantillas/Index.cshtml`
- Create: `Web.UI/Views/Plantillas/Permisos.cshtml`
- Create: `Web.UI/wwwroot/js/plantillas.js`

**Interfaces:**
- Consumes: `IPlantillaApiClient`, `IModuloApiClient` (Task 12); `pintarGrillaPermisos`,
  `leerGrillaPermisos` (Task 14, `permisos.js`).
- Produces: `GET /Plantillas`, CRUD Json proxy, `GET /Plantillas/Permisos?plantillaId=`.

- [ ] **Step 1: `PlantillasController`**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.ApiClient.Clientes;
using Web.ApiClient.Dtos.Permiso;
using Web.UI.Authorization;

namespace Web.UI.Controllers
{
    [Authorize]
    public class PlantillasController : Controller
    {
        private readonly IPlantillaApiClient _plantillas;
        private readonly IModuloApiClient _modulos;

        public PlantillasController(IPlantillaApiClient plantillas, IModuloApiClient modulos)
        {
            _plantillas = plantillas;
            _modulos = modulos;
        }

        [RequierePermiso("Usuarios", Accion.Ver)]
        public IActionResult Index() => View();

        [HttpGet]
        [RequierePermiso("Usuarios", Accion.Ver)]
        public async Task<IActionResult> ObtenerTodos() => Json(await _plantillas.ObtenerTodoAsync());

        [HttpPost]
        [RequierePermiso("Usuarios", Accion.Agregar)]
        public async Task<IActionResult> Crear([FromBody] PlantillaCrearDTO dto) => Json(await _plantillas.InsertarAsync(dto));

        [HttpPost]
        [RequierePermiso("Usuarios", Accion.Actualizar)]
        public async Task<IActionResult> Editar(int id, [FromBody] PlantillaActualizarDTO dto) => Json(await _plantillas.ActualizarAsync(id, dto));

        [HttpPost]
        [RequierePermiso("Usuarios", Accion.Eliminar)]
        public async Task<IActionResult> Eliminar(int id) => Json(await _plantillas.EliminarAsync(id));

        [RequierePermiso("Usuarios", Accion.Ver)]
        public IActionResult Permisos(int plantillaId) => View();

        [HttpGet]
        [RequierePermiso("Usuarios", Accion.Ver)]
        public async Task<IActionResult> ObtenerModulos() => Json(await _modulos.ObtenerTodoAsync());

        [HttpGet]
        [RequierePermiso("Usuarios", Accion.Ver)]
        public async Task<IActionResult> ObtenerPermisos(int plantillaId) => Json(await _plantillas.ObtenerPermisosAsync(plantillaId));

        public class GuardarPermisosRequest
        {
            public int PlantillaId { get; set; }
            public List<PermisoModuloDTO> Matriz { get; set; } = new();
        }

        [HttpPost]
        [RequierePermiso("Usuarios", Accion.Actualizar)]
        public async Task<IActionResult> GuardarPermisos([FromBody] GuardarPermisosRequest body) =>
            Json(await _plantillas.GuardarPermisosAsync(body.PlantillaId, body.Matriz));
    }
}
```

- [ ] **Step 2: `Views/Plantillas/Index.cshtml`**

```html
@{
    ViewData["Title"] = "Plantillas de permisos";
}

<div class="d-flex justify-content-between align-items-center mb-3">
    <h3 class="mb-0">Plantillas de permisos</h3>
    <button type="button" class="btn btn-primary" id="btnNuevo">
        <i class="fa-solid fa-plus me-1"></i>Nueva
    </button>
</div>

<div class="card card-modulo">
    <div class="card-body">
        <div class="table-responsive">
            <table id="tblPlantillas" class="table table-hover align-middle w-100">
                <thead>
                    <tr>
                        <th>Nombre</th>
                        <th class="text-end">Acciones</th>
                    </tr>
                </thead>
                <tbody></tbody>
            </table>
        </div>
    </div>
</div>

<div class="modal fade" id="modalFormulario" tabindex="-1" aria-hidden="true">
    <div class="modal-dialog modal-dialog-centered">
        <div class="modal-content">
            <div class="modal-header">
                <h5 class="modal-title" id="tituloModal">Nueva plantilla</h5>
                <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
            </div>
            <div class="modal-body">
                <form id="formPlantilla">
                    <input type="hidden" id="plantillaIdActual" />
                    <label class="form-label">Nombre</label>
                    <input name="Nombre" id="Nombre" class="form-control" required />
                </form>
            </div>
            <div class="modal-footer">
                <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancelar</button>
                <button type="button" class="btn btn-primary" id="btnGuardar">Guardar</button>
            </div>
        </div>
    </div>
</div>

@section Scripts {
    <script src="~/js/plantillas.js" asp-append-version="true"></script>
}
```

- [ ] **Step 3: `wwwroot/js/plantillas.js`**

```javascript
$(function () {
    const tabla = $('#tblPlantillas').DataTable({
        ajax: { url: '/Plantillas/ObtenerTodos', dataSrc: App.dataSrcTabla },
        columns: [
            { data: 'nombre' },
            {
                data: 'id', orderable: false, className: 'text-end',
                render: id => `
                    <a class="btn btn-sm btn-outline-primary" href="/Plantillas/Permisos?plantillaId=${id}"><i class="fa-solid fa-key"></i></a>
                    <button class="btn btn-sm btn-outline-secondary btn-editar" data-id="${id}"><i class="fa-solid fa-pen"></i></button>
                    <button class="btn btn-sm btn-outline-danger btn-eliminar" data-id="${id}"><i class="fa-solid fa-trash"></i></button>
                `
            }
        ],
        language: App.datatableEsEs
    });

    function recargarTabla() { tabla.ajax.reload(null, false); }

    $('#btnNuevo').on('click', function () {
        $('#tituloModal').text('Nueva plantilla');
        $('#plantillaIdActual').val('');
        $('#formPlantilla')[0].reset();
        new bootstrap.Modal('#modalFormulario').show();
    });

    $('#tblPlantillas').on('click', '.btn-editar', async function () {
        const id = $(this).data('id');
        const respuesta = await $.get(`/Plantillas/ObtenerTodos`);
        const plantilla = (respuesta.resultado ? respuesta.dato : []).find(p => (p.id ?? p.Id) === id);
        $('#tituloModal').text('Editar plantilla');
        $('#plantillaIdActual').val(id);
        $('#Nombre').val(plantilla ? (plantilla.nombre ?? plantilla.Nombre) : '');
        new bootstrap.Modal('#modalFormulario').show();
    });

    $('#tblPlantillas').on('click', '.btn-eliminar', async function () {
        const id = $(this).data('id');
        const confirmado = await App.confirmarEliminar('Se eliminará la plantilla seleccionada.');
        if (!confirmado) return;

        const respuesta = await App.enviarJson(`/Plantillas/Eliminar?id=${id}`, 'POST', {});
        if (!respuesta.resultado) {
            App.mostrarError(respuesta.mensaje);
            return;
        }
        App.mostrarExito('Plantilla eliminada correctamente.');
        recargarTabla();
    });

    $('#btnGuardar').on('click', async function () {
        const id = $('#plantillaIdActual').val();
        const datos = App.recolectarFormulario('#formPlantilla');
        const url = id ? `/Plantillas/Editar?id=${id}` : '/Plantillas/Crear';

        const respuesta = await App.enviarJson(url, 'POST', datos);
        if (!respuesta.resultado) {
            App.mostrarError(respuesta.mensaje);
            return;
        }
        bootstrap.Modal.getInstance(document.getElementById('modalFormulario')).hide();
        App.mostrarExito(id ? 'Plantilla actualizada correctamente.' : 'Plantilla creada correctamente.');
        recargarTabla();
    });
});
```

- [ ] **Step 4: `Views/Plantillas/Permisos.cshtml`** (misma forma que
  `Views/Usuarios/Permisos.cshtml` de Task 14, sin el selector de "Aplicar plantilla" — una
  plantilla no se aplica a sí misma)

```html
@{
    ViewData["Title"] = "Permisos de la plantilla";
    var plantillaId = Context.Request.Query["plantillaId"];
}

<div class="d-flex justify-content-between align-items-center mb-3">
    <h3 class="mb-0">Permisos de la plantilla</h3>
    <a asp-controller="Plantillas" asp-action="Index" class="btn btn-sm btn-outline-secondary">
        <i class="fa-solid fa-arrow-left me-1"></i>Volver
    </a>
</div>

<div class="card card-modulo">
    <div class="card-body">
        <div class="table-responsive">
            <table id="tblGrillaPermisos" class="table table-sm table-hover align-middle">
                <thead>
                    <tr>
                        <th>Módulo</th>
                        <th class="text-center">Ver</th>
                        <th class="text-center">Agregar</th>
                        <th class="text-center">Actualizar</th>
                        <th class="text-center">Eliminar</th>
                    </tr>
                </thead>
                <tbody></tbody>
            </table>
        </div>

        <div class="text-end mt-3">
            <button type="button" class="btn btn-primary" id="btnGuardarPermisos">
                <i class="fa-solid fa-floppy-disk me-1"></i>Guardar
            </button>
        </div>
    </div>
</div>

<input type="hidden" id="plantillaIdActual" value="@plantillaId" />

@section Scripts {
    <script src="~/js/permisos.js" asp-append-version="true"></script>
    <script>
        $(function () {
            const plantillaId = $('#plantillaIdActual').val();

            async function cargar() {
                const [modulosResp, permisosResp] = await Promise.all([
                    $.get('/Plantillas/ObtenerModulos'),
                    $.get('/Plantillas/ObtenerPermisos', { plantillaId })
                ]);
                const modulos = (modulosResp.resultado && modulosResp.dato) ? modulosResp.dato : [];
                const matriz = (permisosResp.resultado && permisosResp.dato) ? permisosResp.dato : [];
                $('#tblGrillaPermisos tbody').html(pintarGrillaPermisos(modulos, matriz));
            }

            $(document).on('click', '#btnGuardarPermisos', async function () {
                const matriz = leerGrillaPermisos();
                const respuesta = await App.enviarJson('/Plantillas/GuardarPermisos', 'POST', { PlantillaId: Number(plantillaId), Matriz: matriz });
                if (!respuesta.resultado) {
                    App.mostrarError(respuesta.mensaje);
                    return;
                }
                App.mostrarExito('Permisos guardados correctamente.');
            });

            cargar();
        });
    </script>
}
```

- [ ] **Step 5: Build**

```powershell
cd C:\Users\migue\source\repos\angelm0508\Web
dotnet build Web.slnx -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\webbuild\"
```

- [ ] **Step 6: Commit**

```bash
git add Web.UI/Controllers/PlantillasController.cs Web.UI/Views/Plantillas/Index.cshtml Web.UI/Views/Plantillas/Permisos.cshtml Web.UI/wwwroot/js/plantillas.js
git commit -m "feat(permisos): CRUD de plantillas y su grilla de permisos

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 16: Menú (`_Layout.cshtml`) respeta los permisos

**Files:**
- Modify: `Web.UI/Views/_ViewImports.cshtml`
- Modify: `Web.UI/Views/Shared/_Layout.cshtml`

**Interfaces:**
- Consumes: `ClaimsPrincipal.TienePermiso` (Task 11).

- [ ] **Step 1: Agregar el `using` global**

En `Web.UI/Views/_ViewImports.cshtml`, agregar una línea:

```
@using Web.UI.Authorization
```

- [ ] **Step 2: Reemplazar el `<nav class="sidebar-nav">` de `_Layout.cshtml`**

Reemplazar todo el bloque `<nav class="sidebar-nav">...</nav>` (y las variables `EsActivoXxx` del
`@{ }` de arriba) por:

```razor
@{
    var rutaActual = $"{ViewContext.RouteData.Values["controller"]}";
    var accionActual = $"{ViewContext.RouteData.Values["action"]}";
    bool EsActivo(string controlador) => string.Equals(rutaActual, controlador, StringComparison.OrdinalIgnoreCase);
    bool EsActivoAccion(string controlador, string accion) => EsActivo(controlador) && string.Equals(accionActual, accion, StringComparison.OrdinalIgnoreCase);
    bool Puede(string codigoModulo) => User.TienePermiso(codigoModulo, Accion.Ver);
    bool EsActivoInventario = new[] { "Articulos", "Fabricantes", "GruposArticulo", "UnidadesMedida", "GruposUnidadMedida", "Almacenes", "Existencias", "EntradasMercancia", "SalidasMercancia" }.Any(EsActivo);
    bool EsActivoSociosNegocioGrupo = new[] { "SociosNegocio", "GruposSn", "ListadosPrecio" }.Any(EsActivo);
    bool EsActivoVentas = new[] { "Cotizaciones", "Pedidos", "Entregas", "Facturas" }.Any(EsActivo);
    bool EsActivoCompras = new[] { "PedidosCompra", "EntregasCompra", "FacturasCompra" }.Any(EsActivo);
    bool EsActivoAdministracion = new[] { "Usuarios", "Plantillas" }.Any(EsActivo);
    bool MuestraInventario = new[] { "Articulos", "Fabricantes", "GruposArticulo", "UnidadesMedida", "GruposUnidadMedida", "Almacenes", "Existencias", "EntradasMercancia", "SalidasMercancia" }.Any(Puede);
    bool MuestraSociosNegocio = new[] { "SociosNegocio", "GruposSn", "ListadosPrecio" }.Any(Puede);
    bool MuestraVentas = new[] { "Cotizaciones", "Pedidos", "Entregas", "Facturas" }.Any(Puede);
    bool MuestraCompras = new[] { "PedidosCompra", "EntregasCompra", "FacturasCompra" }.Any(Puede);
    bool MuestraAdministracion = Puede("Usuarios");
    var csrfToken = Antiforgery.GetAndStoreTokens(Context).RequestToken;
}
```

```html
<nav class="sidebar-nav">
    <a class="nav-link @(EsActivo("Home") ? "active" : "")" asp-controller="Home" asp-action="Index">
        <i class="fa-solid fa-gauge-high"></i><span>Dashboard</span>
    </a>

    @if (MuestraInventario)
    {
        <a class="nav-link nav-link-toggle @(EsActivoInventario ? "active" : "")" data-bs-toggle="collapse" href="#submenuInventario" role="button" aria-expanded="@(EsActivoInventario ? "true" : "false")" aria-controls="submenuInventario">
            <i class="fa-solid fa-boxes-stacked"></i><span>Inventario</span>
            <i class="fa-solid fa-chevron-down ms-auto submenu-caret"></i>
        </a>
        <div class="collapse @(EsActivoInventario ? "show" : "")" id="submenuInventario">
            @if (Puede("Articulos"))
            {
                <a class="nav-link nav-sublink @(EsActivo("Articulos") ? "active" : "")" asp-controller="Articulos" asp-action="Index">
                    <i class="fa-solid fa-box"></i><span>Artículos</span>
                </a>
            }
            @if (Puede("Fabricantes"))
            {
                <a class="nav-link nav-sublink @(EsActivo("Fabricantes") ? "active" : "")" asp-controller="Fabricantes" asp-action="Index">
                    <i class="fa-solid fa-industry"></i><span>Fabricantes</span>
                </a>
            }
            @if (Puede("GruposArticulo"))
            {
                <a class="nav-link nav-sublink @(EsActivo("GruposArticulo") ? "active" : "")" asp-controller="GruposArticulo" asp-action="Index">
                    <i class="fa-solid fa-layer-group"></i><span>Grupos</span>
                </a>
            }
            @if (Puede("UnidadesMedida"))
            {
                <a class="nav-link nav-sublink @(EsActivo("UnidadesMedida") ? "active" : "")" asp-controller="UnidadesMedida" asp-action="Index">
                    <i class="fa-solid fa-ruler-combined"></i><span>Unidades de medida</span>
                </a>
            }
            @if (Puede("GruposUnidadMedida"))
            {
                <a class="nav-link nav-sublink @(EsActivo("GruposUnidadMedida") ? "active" : "")" asp-controller="GruposUnidadMedida" asp-action="Index">
                    <i class="fa-solid fa-boxes-packing"></i><span>Grupo unidades de medida</span>
                </a>
            }
            @if (Puede("Almacenes"))
            {
                <a class="nav-link nav-sublink @(EsActivo("Almacenes") ? "active" : "")" asp-controller="Almacenes" asp-action="Index">
                    <i class="fa-solid fa-warehouse"></i><span>Almacenes</span>
                </a>
            }
            @if (Puede("Existencias"))
            {
                <a class="nav-link nav-sublink @(EsActivo("Existencias") ? "active" : "")" asp-controller="Existencias" asp-action="Index">
                    <i class="fa-solid fa-boxes-stacked"></i><span>Existencias</span>
                </a>
            }
            @if (Puede("EntradasMercancia"))
            {
                <a class="nav-link nav-sublink @(EsActivo("EntradasMercancia") ? "active" : "")" asp-controller="EntradasMercancia" asp-action="Index">
                    <i class="fa-solid fa-arrow-right-to-bracket"></i><span>Entradas de mercancía</span>
                </a>
            }
            @if (Puede("SalidasMercancia"))
            {
                <a class="nav-link nav-sublink @(EsActivo("SalidasMercancia") ? "active" : "")" asp-controller="SalidasMercancia" asp-action="Index">
                    <i class="fa-solid fa-arrow-right-from-bracket"></i><span>Salidas de mercancía</span>
                </a>
            }
        </div>
    }

    @if (MuestraSociosNegocio)
    {
        <a class="nav-link nav-link-toggle @(EsActivoSociosNegocioGrupo ? "active" : "")" data-bs-toggle="collapse" href="#submenuSociosNegocio" role="button" aria-expanded="@(EsActivoSociosNegocioGrupo ? "true" : "false")" aria-controls="submenuSociosNegocio">
            <i class="fa-solid fa-handshake"></i><span>Socios de Negocio</span>
            <i class="fa-solid fa-chevron-down ms-auto submenu-caret"></i>
        </a>
        <div class="collapse @(EsActivoSociosNegocioGrupo ? "show" : "")" id="submenuSociosNegocio">
            @if (Puede("SociosNegocio"))
            {
                <a class="nav-link nav-sublink @(EsActivo("SociosNegocio") ? "active" : "")" asp-controller="SociosNegocio" asp-action="Index">
                    <i class="fa-solid fa-handshake"></i><span>Socios de Negocio</span>
                </a>
            }
            @if (Puede("GruposSn"))
            {
                <a class="nav-link nav-sublink nav-link-toggle @(EsActivo("GruposSn") ? "active" : "")" data-bs-toggle="collapse" href="#submenuGruposSn" role="button" aria-expanded="@(EsActivo("GruposSn") ? "true" : "false")" aria-controls="submenuGruposSn">
                    <i class="fa-solid fa-people-group"></i><span>Grupos SN</span>
                    <i class="fa-solid fa-chevron-down ms-auto submenu-caret"></i>
                </a>
                <div class="collapse @(EsActivo("GruposSn") ? "show" : "")" id="submenuGruposSn">
                    <a class="nav-link nav-sublink2 @(EsActivoAccion("GruposSn", "Clientes") ? "active" : "")" asp-controller="GruposSn" asp-action="Clientes">
                        <i class="fa-solid fa-user"></i><span>Clientes</span>
                    </a>
                    <a class="nav-link nav-sublink2 @(EsActivoAccion("GruposSn", "Proveedores") ? "active" : "")" asp-controller="GruposSn" asp-action="Proveedores">
                        <i class="fa-solid fa-truck-field"></i><span>Proveedores</span>
                    </a>
                </div>
            }
            @if (Puede("ListadosPrecio"))
            {
                <a class="nav-link nav-sublink @(EsActivo("ListadosPrecio") ? "active" : "")" asp-controller="ListadosPrecio" asp-action="Index">
                    <i class="fa-solid fa-tags"></i><span>Listado de Precios</span>
                </a>
            }
        </div>
    }

    @if (Puede("NumeracionDocumento"))
    {
        <div class="nav-section-title">Documentos</div>
        <a class="nav-link @(EsActivo("NumeracionDocumento") ? "active" : "")" asp-controller="NumeracionDocumento" asp-action="Index">
            <i class="fa-solid fa-hashtag"></i><span>Numeración de documentos</span>
        </a>
    }

    @if (MuestraVentas)
    {
        <a class="nav-link nav-link-toggle @(EsActivoVentas ? "active" : "")" data-bs-toggle="collapse" href="#submenuVentas" role="button" aria-expanded="@(EsActivoVentas ? "true" : "false")" aria-controls="submenuVentas">
            <i class="fa-solid fa-cart-shopping"></i><span>Ventas</span>
            <i class="fa-solid fa-chevron-down ms-auto submenu-caret"></i>
        </a>
        <div class="collapse @(EsActivoVentas ? "show" : "")" id="submenuVentas">
            @if (Puede("Cotizaciones"))
            {
                <a class="nav-link nav-sublink @(EsActivo("Cotizaciones") ? "active" : "")" asp-controller="Cotizaciones" asp-action="Index">
                    <i class="fa-solid fa-file-invoice"></i><span>Cotizaciones</span>
                </a>
            }
            @if (Puede("Pedidos"))
            {
                <a class="nav-link nav-sublink @(EsActivo("Pedidos") ? "active" : "")" asp-controller="Pedidos" asp-action="Index">
                    <i class="fa-solid fa-cart-arrow-down"></i><span>Pedidos</span>
                </a>
            }
            @if (Puede("Entregas"))
            {
                <a class="nav-link nav-sublink @(EsActivo("Entregas") ? "active" : "")" asp-controller="Entregas" asp-action="Index">
                    <i class="fa-solid fa-truck"></i><span>Entregas</span>
                </a>
            }
            @if (Puede("Facturas"))
            {
                <a class="nav-link nav-sublink @(EsActivo("Facturas") ? "active" : "")" asp-controller="Facturas" asp-action="Index">
                    <i class="fa-solid fa-file-invoice-dollar"></i><span>Facturas</span>
                </a>
            }
        </div>
    }

    @if (MuestraCompras)
    {
        <a class="nav-link nav-link-toggle @(EsActivoCompras ? "active" : "")" data-bs-toggle="collapse" href="#submenuCompras" role="button" aria-expanded="@(EsActivoCompras ? "true" : "false")" aria-controls="submenuCompras">
            <i class="fa-solid fa-cart-flatbed"></i><span>Compras</span>
            <i class="fa-solid fa-chevron-down ms-auto submenu-caret"></i>
        </a>
        <div class="collapse @(EsActivoCompras ? "show" : "")" id="submenuCompras">
            @if (Puede("PedidosCompra"))
            {
                <a class="nav-link nav-sublink @(EsActivo("PedidosCompra") ? "active" : "")" asp-controller="PedidosCompra" asp-action="Index">
                    <i class="fa-solid fa-cart-arrow-down"></i><span>Pedidos de compra</span>
                </a>
            }
            @if (Puede("EntregasCompra"))
            {
                <a class="nav-link nav-sublink @(EsActivo("EntregasCompra") ? "active" : "")" asp-controller="EntregasCompra" asp-action="Index">
                    <i class="fa-solid fa-truck-ramp-box"></i><span>Entregas de compra</span>
                </a>
            }
            @if (Puede("FacturasCompra"))
            {
                <a class="nav-link nav-sublink @(EsActivo("FacturasCompra") ? "active" : "")" asp-controller="FacturasCompra" asp-action="Index">
                    <i class="fa-solid fa-file-invoice-dollar"></i><span>Facturas de compra</span>
                </a>
            }
        </div>
    }

    @if (MuestraAdministracion)
    {
        <a class="nav-link nav-link-toggle @(EsActivoAdministracion ? "active" : "")" data-bs-toggle="collapse" href="#submenuAdministracion" role="button" aria-expanded="@(EsActivoAdministracion ? "true" : "false")" aria-controls="submenuAdministracion">
            <i class="fa-solid fa-user-shield"></i><span>Administración</span>
            <i class="fa-solid fa-chevron-down ms-auto submenu-caret"></i>
        </a>
        <div class="collapse @(EsActivoAdministracion ? "show" : "")" id="submenuAdministracion">
            <a class="nav-link nav-sublink @(EsActivo("Usuarios") ? "active" : "")" asp-controller="Usuarios" asp-action="Index">
                <i class="fa-solid fa-users"></i><span>Usuarios</span>
            </a>
            <a class="nav-link nav-sublink @(EsActivo("Plantillas") ? "active" : "")" asp-controller="Plantillas" asp-action="Index">
                <i class="fa-solid fa-copy"></i><span>Plantillas de permisos</span>
            </a>
        </div>
    }
</nav>
```

(`MuestraAdministracion` usa `Puede("Usuarios")` directo, sin un array `Any` de un solo elemento,
para que quede igual de legible — `Plantillas` no es un `Modulo` propio, vive bajo el mismo
`"Usuarios"` que ya protege sus controllers en Task 15.)

- [ ] **Step 3: Build**

```powershell
cd C:\Users\migue\source\repos\angelm0508\Web
dotnet build Web.slnx -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\webbuild\"
```

- [ ] **Step 4: Commit**

```bash
git add Web.UI/Views/_ViewImports.cshtml Web.UI/Views/Shared/_Layout.cshtml
git commit -m "feat(permisos): el menu respeta los permisos del usuario

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Fase 5 — Web: decorar los controllers MVC existentes

**Regla mecánica única para Tasks 17-19** (igual que la Fase 2 de la API, adaptada a MVC): en
cada archivo de la tabla, agregar `using Web.UI.Authorization;` si no está, y colocar
`[RequierePermiso("<CodigoModulo>", Accion.<Accion>)]` inmediatamente encima de cada método de
acción pública (sobre el `[HttpGet]`/`[HttpPost]` si lo tiene, o directamente encima de la firma
del método si no lleva ningún atributo HTTP explícito — ASP.NET MVC permite GET implícito en
métodos sin atributo, como `Index()` o `GruposSnController.Clientes()`/`Proveedores()`). Sin
tests automatizados de Web — verificación por `dotnet build` limpio.

### Task 17: Decorar controllers de Ventas y Compras (7 archivos)

**Files:** los 7 listados en la tabla. Ejemplo:

```csharp
[HttpPost]
[RequierePermiso("Cotizaciones", Accion.Agregar)]
public async Task<IActionResult> Crear([FromBody] CotizacionCrearDTO dto)
```

- [ ] **Step 1: Aplicar la transformación según la tabla**

| Archivo | Método | Accion |
|---|---|---|
| `CotizacionesController.cs` | `Index` | `Ver` (módulo `Cotizaciones`) |
| `CotizacionesController.cs` | `ObtenerTodos` | `Ver` |
| `CotizacionesController.cs` | `FormularioCrear` | `Agregar` |
| `CotizacionesController.cs` | `FormularioEditar` | `Actualizar` |
| `CotizacionesController.cs` | `Crear` | `Agregar` |
| `CotizacionesController.cs` | `Editar` | `Actualizar` |
| `CotizacionesController.cs` | `Eliminar` | `Eliminar` |
| `CotizacionesController.cs` | `ObtenerDetalle` | `Ver` |
| `CotizacionesController.cs` | `BuscarSocios` | `Ver` |
| `CotizacionesController.cs` | `BuscarArticulos` | `Ver` |
| `CotizacionesController.cs` | `BuscarArticulosPorCodigo` | `Ver` |
| `CotizacionesController.cs` | `BuscarAlmacenes` | `Ver` |
| `CotizacionesController.cs` | `BuscarImpuestos` | `Ver` |
| `CotizacionesController.cs` | `ObtenerAlmacenPorCodigo` | `Ver` |
| `CotizacionesController.cs` | `ObtenerImpuestoPorCodigo` | `Ver` |
| `CotizacionesController.cs` | `CrearLinea` | `Agregar` |
| `CotizacionesController.cs` | `EditarLinea` | `Actualizar` |
| `CotizacionesController.cs` | `EliminarLinea` | `Eliminar` |
| `PedidosController.cs` | `Index` | `Ver` (módulo `Pedidos`) |
| `PedidosController.cs` | `ObtenerTodos` | `Ver` |
| `PedidosController.cs` | `FormularioCrear` | `Agregar` |
| `PedidosController.cs` | `FormularioEditar` | `Actualizar` |
| `PedidosController.cs` | `Crear` | `Agregar` |
| `PedidosController.cs` | `Editar` | `Actualizar` |
| `PedidosController.cs` | `Eliminar` | `Eliminar` |
| `PedidosController.cs` | `ObtenerDetalle` | `Ver` |
| `PedidosController.cs` | `BuscarSocios` | `Ver` |
| `PedidosController.cs` | `BuscarArticulos` | `Ver` |
| `PedidosController.cs` | `BuscarArticulosPorCodigo` | `Ver` |
| `PedidosController.cs` | `BuscarAlmacenes` | `Ver` |
| `PedidosController.cs` | `BuscarImpuestos` | `Ver` |
| `PedidosController.cs` | `ObtenerAlmacenPorCodigo` | `Ver` |
| `PedidosController.cs` | `ObtenerImpuestoPorCodigo` | `Ver` |
| `PedidosController.cs` | `CrearLinea` | `Agregar` |
| `PedidosController.cs` | `EditarLinea` | `Actualizar` |
| `PedidosController.cs` | `EliminarLinea` | `Eliminar` |
| `EntregasController.cs` | `Index` | `Ver` (módulo `Entregas`) |
| `EntregasController.cs` | `ObtenerTodos` | `Ver` |
| `EntregasController.cs` | `FormularioCrear` | `Agregar` |
| `EntregasController.cs` | `FormularioEditar` | `Actualizar` |
| `EntregasController.cs` | `Crear` | `Agregar` |
| `EntregasController.cs` | `Editar` | `Actualizar` |
| `EntregasController.cs` | `Eliminar` | `Eliminar` |
| `EntregasController.cs` | `ObtenerDetalle` | `Ver` |
| `EntregasController.cs` | `BuscarSocios` | `Ver` |
| `EntregasController.cs` | `BuscarArticulos` | `Ver` |
| `EntregasController.cs` | `BuscarArticulosPorCodigo` | `Ver` |
| `EntregasController.cs` | `BuscarAlmacenes` | `Ver` |
| `EntregasController.cs` | `BuscarImpuestos` | `Ver` |
| `EntregasController.cs` | `ObtenerAlmacenPorCodigo` | `Ver` |
| `EntregasController.cs` | `ObtenerImpuestoPorCodigo` | `Ver` |
| `EntregasController.cs` | `CrearLinea` | `Agregar` |
| `EntregasController.cs` | `EditarLinea` | `Actualizar` |
| `EntregasController.cs` | `EliminarLinea` | `Eliminar` |
| `FacturasController.cs` | `Index` | `Ver` (módulo `Facturas`) |
| `FacturasController.cs` | `ObtenerTodos` | `Ver` |
| `FacturasController.cs` | `FormularioCrear` | `Agregar` |
| `FacturasController.cs` | `FormularioEditar` | `Actualizar` |
| `FacturasController.cs` | `Crear` | `Agregar` |
| `FacturasController.cs` | `Editar` | `Actualizar` |
| `FacturasController.cs` | `Eliminar` | `Eliminar` |
| `FacturasController.cs` | `ObtenerDetalle` | `Ver` |
| `FacturasController.cs` | `CrearLinea` | `Agregar` |
| `FacturasController.cs` | `EditarLinea` | `Actualizar` |
| `FacturasController.cs` | `EliminarLinea` | `Eliminar` |
| `FacturasController.cs` | `BuscarSocios` | `Ver` |
| `FacturasController.cs` | `BuscarArticulos` | `Ver` |
| `FacturasController.cs` | `BuscarArticulosPorCodigo` | `Ver` |
| `FacturasController.cs` | `BuscarAlmacenes` | `Ver` |
| `FacturasController.cs` | `BuscarImpuestos` | `Ver` |
| `FacturasController.cs` | `ObtenerAlmacenPorCodigo` | `Ver` |
| `FacturasController.cs` | `ObtenerImpuestoPorCodigo` | `Ver` |
| `PedidosCompraController.cs` | `Index` | `Ver` (módulo `PedidosCompra`) |
| `PedidosCompraController.cs` | `ObtenerTodos` | `Ver` |
| `PedidosCompraController.cs` | `FormularioCrear` | `Agregar` |
| `PedidosCompraController.cs` | `FormularioEditar` | `Actualizar` |
| `PedidosCompraController.cs` | `Crear` | `Agregar` |
| `PedidosCompraController.cs` | `Editar` | `Actualizar` |
| `PedidosCompraController.cs` | `Eliminar` | `Eliminar` |
| `PedidosCompraController.cs` | `ObtenerDetalle` | `Ver` |
| `PedidosCompraController.cs` | `BuscarSocios` | `Ver` |
| `PedidosCompraController.cs` | `BuscarArticulos` | `Ver` |
| `PedidosCompraController.cs` | `BuscarArticulosPorCodigo` | `Ver` |
| `PedidosCompraController.cs` | `BuscarAlmacenes` | `Ver` |
| `PedidosCompraController.cs` | `BuscarImpuestos` | `Ver` |
| `PedidosCompraController.cs` | `ObtenerAlmacenPorCodigo` | `Ver` |
| `PedidosCompraController.cs` | `ObtenerImpuestoPorCodigo` | `Ver` |
| `PedidosCompraController.cs` | `CrearLinea` | `Agregar` |
| `PedidosCompraController.cs` | `EditarLinea` | `Actualizar` |
| `PedidosCompraController.cs` | `EliminarLinea` | `Eliminar` |
| `EntregasCompraController.cs` | `Index` | `Ver` (módulo `EntregasCompra`) |
| `EntregasCompraController.cs` | `ObtenerTodos` | `Ver` |
| `EntregasCompraController.cs` | `FormularioCrear` | `Agregar` |
| `EntregasCompraController.cs` | `FormularioEditar` | `Actualizar` |
| `EntregasCompraController.cs` | `Crear` | `Agregar` |
| `EntregasCompraController.cs` | `Editar` | `Actualizar` |
| `EntregasCompraController.cs` | `Eliminar` | `Eliminar` |
| `EntregasCompraController.cs` | `ObtenerDetalle` | `Ver` |
| `EntregasCompraController.cs` | `BuscarSocios` | `Ver` |
| `EntregasCompraController.cs` | `BuscarArticulos` | `Ver` |
| `EntregasCompraController.cs` | `BuscarArticulosPorCodigo` | `Ver` |
| `EntregasCompraController.cs` | `BuscarAlmacenes` | `Ver` |
| `EntregasCompraController.cs` | `BuscarImpuestos` | `Ver` |
| `EntregasCompraController.cs` | `ObtenerAlmacenPorCodigo` | `Ver` |
| `EntregasCompraController.cs` | `ObtenerImpuestoPorCodigo` | `Ver` |
| `EntregasCompraController.cs` | `CrearLinea` | `Agregar` |
| `EntregasCompraController.cs` | `EditarLinea` | `Actualizar` |
| `EntregasCompraController.cs` | `EliminarLinea` | `Eliminar` |
| `FacturasCompraController.cs` | `Index` | `Ver` (módulo `FacturasCompra`) |
| `FacturasCompraController.cs` | `ObtenerTodos` | `Ver` |
| `FacturasCompraController.cs` | `FormularioCrear` | `Agregar` |
| `FacturasCompraController.cs` | `FormularioEditar` | `Actualizar` |
| `FacturasCompraController.cs` | `Crear` | `Agregar` |
| `FacturasCompraController.cs` | `Editar` | `Actualizar` |
| `FacturasCompraController.cs` | `Eliminar` | `Eliminar` |
| `FacturasCompraController.cs` | `ObtenerDetalle` | `Ver` |
| `FacturasCompraController.cs` | `CrearLinea` | `Agregar` |
| `FacturasCompraController.cs` | `EditarLinea` | `Actualizar` |
| `FacturasCompraController.cs` | `EliminarLinea` | `Eliminar` |
| `FacturasCompraController.cs` | `BuscarSocios` | `Ver` |
| `FacturasCompraController.cs` | `BuscarArticulos` | `Ver` |
| `FacturasCompraController.cs` | `BuscarArticulosPorCodigo` | `Ver` |
| `FacturasCompraController.cs` | `BuscarAlmacenes` | `Ver` |
| `FacturasCompraController.cs` | `BuscarImpuestos` | `Ver` |
| `FacturasCompraController.cs` | `ObtenerAlmacenPorCodigo` | `Ver` |
| `FacturasCompraController.cs` | `ObtenerImpuestoPorCodigo` | `Ver` |

- [ ] **Step 2: Build**

```powershell
cd C:\Users\migue\source\repos\angelm0508\Web
dotnet build Web.slnx -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\webbuild\"
```

- [ ] **Step 3: Commit**

```bash
git add Web.UI/Controllers/CotizacionesController.cs Web.UI/Controllers/PedidosController.cs Web.UI/Controllers/EntregasController.cs Web.UI/Controllers/FacturasController.cs Web.UI/Controllers/PedidosCompraController.cs Web.UI/Controllers/EntregasCompraController.cs Web.UI/Controllers/FacturasCompraController.cs
git commit -m "feat(permisos): decora los controllers Web de Ventas y Compras con RequierePermiso

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 18: Decorar controllers Web de Inventario (9 archivos)

**Files:** los 9 listados en la tabla. Misma transformación mecánica que Task 17.

- [ ] **Step 1: Aplicar la transformación según la tabla**

| Archivo | Método | Accion |
|---|---|---|
| `AlmacenesController.cs` | `Index` | `Ver` (módulo `Almacenes`) |
| `AlmacenesController.cs` | `ObtenerTodos` | `Ver` |
| `AlmacenesController.cs` | `FormularioCrear` | `Agregar` |
| `AlmacenesController.cs` | `FormularioEditar` | `Actualizar` |
| `AlmacenesController.cs` | `Crear` | `Agregar` |
| `AlmacenesController.cs` | `Editar` | `Actualizar` |
| `AlmacenesController.cs` | `Eliminar` | `Eliminar` |
| `ArticulosController.cs` | `Index` | `Ver` (módulo `Articulos`) |
| `ArticulosController.cs` | `ObtenerTodos` | `Ver` |
| `ArticulosController.cs` | `Crear` (las 2 sobrecargas: `Crear()` GET y `Crear(ArticuloCrearDTO)` POST) | `Agregar` |
| `ArticulosController.cs` | `FormularioEditar` | `Actualizar` |
| `ArticulosController.cs` | `Editar` | `Actualizar` |
| `ArticulosController.cs` | `Eliminar` | `Eliminar` |
| `ArticulosController.cs` | `BuscarAlmacenes` | `Ver` |
| `ArticulosController.cs` | `ObtenerAlmacenPorCodigo` | `Ver` |
| `EntradasMercanciaController.cs` | `Index` | `Ver` (módulo `EntradasMercancia`) |
| `EntradasMercanciaController.cs` | `ObtenerTodos` | `Ver` |
| `EntradasMercanciaController.cs` | `FormularioCrear` | `Agregar` |
| `EntradasMercanciaController.cs` | `FormularioEditar` | `Actualizar` |
| `EntradasMercanciaController.cs` | `Crear` | `Agregar` |
| `EntradasMercanciaController.cs` | `Editar` | `Actualizar` |
| `EntradasMercanciaController.cs` | `Eliminar` | `Eliminar` |
| `EntradasMercanciaController.cs` | `ObtenerDetalle` | `Ver` |
| `EntradasMercanciaController.cs` | `BuscarArticulos` | `Ver` |
| `EntradasMercanciaController.cs` | `BuscarAlmacenes` | `Ver` |
| `EntradasMercanciaController.cs` | `ObtenerAlmacenPorCodigo` | `Ver` |
| `ExistenciasController.cs` | `Index` | `Ver` (módulo `Existencias`) |
| `ExistenciasController.cs` | `ObtenerTodos` | `Ver` |
| `ExistenciasController.cs` | `BuscarArticulos` | `Ver` |
| `ExistenciasController.cs` | `Kardex` | `Ver` |
| `FabricantesController.cs` | `Index` | `Ver` (módulo `Fabricantes`) |
| `FabricantesController.cs` | `ObtenerTodos` | `Ver` |
| `FabricantesController.cs` | `FormularioCrear` | `Agregar` |
| `FabricantesController.cs` | `FormularioEditar` | `Actualizar` |
| `FabricantesController.cs` | `Crear` | `Agregar` |
| `FabricantesController.cs` | `Editar` | `Actualizar` |
| `FabricantesController.cs` | `Eliminar` | `Eliminar` |
| `GruposArticuloController.cs` | `Index` | `Ver` (módulo `GruposArticulo`) |
| `GruposArticuloController.cs` | `ObtenerTodos` | `Ver` |
| `GruposArticuloController.cs` | `FormularioCrear` | `Agregar` |
| `GruposArticuloController.cs` | `FormularioEditar` | `Actualizar` |
| `GruposArticuloController.cs` | `Crear` | `Agregar` |
| `GruposArticuloController.cs` | `Editar` | `Actualizar` |
| `GruposArticuloController.cs` | `Eliminar` | `Eliminar` |
| `GruposUnidadMedidaController.cs` | `Index` | `Ver` (módulo `GruposUnidadMedida`) |
| `GruposUnidadMedidaController.cs` | `ObtenerTodos` | `Ver` |
| `GruposUnidadMedidaController.cs` | `FormularioCrear` | `Agregar` |
| `GruposUnidadMedidaController.cs` | `FormularioEditar` | `Actualizar` |
| `GruposUnidadMedidaController.cs` | `Crear` | `Agregar` |
| `GruposUnidadMedidaController.cs` | `Editar` | `Actualizar` |
| `GruposUnidadMedidaController.cs` | `Eliminar` | `Eliminar` |
| `GruposUnidadMedidaController.cs` | `ObtenerDetalle` | `Ver` |
| `GruposUnidadMedidaController.cs` | `CrearLinea` | `Agregar` |
| `GruposUnidadMedidaController.cs` | `EditarLinea` | `Actualizar` |
| `GruposUnidadMedidaController.cs` | `EliminarLinea` | `Eliminar` |
| `SalidasMercanciaController.cs` | `Index` | `Ver` (módulo `SalidasMercancia`) |
| `SalidasMercanciaController.cs` | `ObtenerTodos` | `Ver` |
| `SalidasMercanciaController.cs` | `FormularioCrear` | `Agregar` |
| `SalidasMercanciaController.cs` | `FormularioEditar` | `Actualizar` |
| `SalidasMercanciaController.cs` | `Crear` | `Agregar` |
| `SalidasMercanciaController.cs` | `Editar` | `Actualizar` |
| `SalidasMercanciaController.cs` | `Eliminar` | `Eliminar` |
| `SalidasMercanciaController.cs` | `ObtenerDetalle` | `Ver` |
| `SalidasMercanciaController.cs` | `BuscarArticulos` | `Ver` |
| `SalidasMercanciaController.cs` | `BuscarAlmacenes` | `Ver` |
| `SalidasMercanciaController.cs` | `ObtenerAlmacenPorCodigo` | `Ver` |
| `UnidadesMedidaController.cs` | `Index` | `Ver` (módulo `UnidadesMedida`) |
| `UnidadesMedidaController.cs` | `ObtenerTodos` | `Ver` |
| `UnidadesMedidaController.cs` | `FormularioCrear` | `Agregar` |
| `UnidadesMedidaController.cs` | `FormularioEditar` | `Actualizar` |
| `UnidadesMedidaController.cs` | `Crear` | `Agregar` |
| `UnidadesMedidaController.cs` | `Editar` | `Actualizar` |
| `UnidadesMedidaController.cs` | `Eliminar` | `Eliminar` |

- [ ] **Step 2: Build**

```powershell
cd C:\Users\migue\source\repos\angelm0508\Web
dotnet build Web.slnx -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\webbuild\"
```

- [ ] **Step 3: Commit**

```bash
git add Web.UI/Controllers/AlmacenesController.cs Web.UI/Controllers/ArticulosController.cs Web.UI/Controllers/EntradasMercanciaController.cs Web.UI/Controllers/ExistenciasController.cs Web.UI/Controllers/FabricantesController.cs Web.UI/Controllers/GruposArticuloController.cs Web.UI/Controllers/GruposUnidadMedidaController.cs Web.UI/Controllers/SalidasMercanciaController.cs Web.UI/Controllers/UnidadesMedidaController.cs
git commit -m "feat(permisos): decora los controllers Web de Inventario con RequierePermiso

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 19: Decorar controllers Web de Socios de Negocio y Documentos (5 archivos)

**Files:** los 5 listados en la tabla. Misma transformación mecánica que Tasks 17-18.
`DireccionesController.cs` usa el módulo `SociosNegocio` (es el sub-recurso de direcciones del
socio, no un módulo aparte — mismo criterio que en la API, Task 10).

- [ ] **Step 1: Aplicar la transformación según la tabla**

| Archivo | Método | Accion |
|---|---|---|
| `DireccionesController.cs` | `Index` | `Ver` (módulo `SociosNegocio`) |
| `DireccionesController.cs` | `ObtenerTodos` | `Ver` |
| `DireccionesController.cs` | `ObtenerPorSocio` | `Ver` |
| `DireccionesController.cs` | `FormularioCrear` | `Agregar` |
| `DireccionesController.cs` | `FormularioEditar` | `Actualizar` |
| `DireccionesController.cs` | `Crear` | `Agregar` |
| `DireccionesController.cs` | `Editar` | `Actualizar` |
| `DireccionesController.cs` | `Eliminar` | `Eliminar` |
| `GruposSnController.cs` | `Clientes` | `Ver` (módulo `GruposSn`) |
| `GruposSnController.cs` | `Proveedores` | `Ver` |
| `GruposSnController.cs` | `ObtenerTodos` | `Ver` |
| `GruposSnController.cs` | `FormularioCrear` | `Agregar` |
| `GruposSnController.cs` | `FormularioEditar` | `Actualizar` |
| `GruposSnController.cs` | `Crear` | `Agregar` |
| `GruposSnController.cs` | `Editar` | `Actualizar` |
| `GruposSnController.cs` | `Eliminar` | `Eliminar` |
| `ListadosPrecioController.cs` | `Index` | `Ver` (módulo `ListadosPrecio`) |
| `ListadosPrecioController.cs` | `ObtenerTodos` | `Ver` |
| `ListadosPrecioController.cs` | `FormularioCrear` | `Agregar` |
| `ListadosPrecioController.cs` | `FormularioEditar` | `Actualizar` |
| `ListadosPrecioController.cs` | `Crear` | `Agregar` |
| `ListadosPrecioController.cs` | `Editar` | `Actualizar` |
| `ListadosPrecioController.cs` | `Eliminar` | `Eliminar` |
| `NumeracionDocumentoController.cs` | `Index` | `Ver` (módulo `NumeracionDocumento`) |
| `NumeracionDocumentoController.cs` | `ObtenerTodos` | `Ver` |
| `NumeracionDocumentoController.cs` | `FormularioEditar` | `Actualizar` |
| `NumeracionDocumentoController.cs` | `Editar` | `Actualizar` |
| `NumeracionDocumentoController.cs` | `ObtenerDetalle` | `Ver` |
| `NumeracionDocumentoController.cs` | `CrearLinea` | `Agregar` |
| `NumeracionDocumentoController.cs` | `EditarLinea` | `Actualizar` |
| `NumeracionDocumentoController.cs` | `EliminarLinea` | `Eliminar` |
| `SociosNegocioController.cs` | `Index` | `Ver` (módulo `SociosNegocio`) |
| `SociosNegocioController.cs` | `ObtenerTodos` | `Ver` |
| `SociosNegocioController.cs` | `Crear` (las 2 sobrecargas) | `Agregar` |
| `SociosNegocioController.cs` | `FormularioEditar` | `Actualizar` |
| `SociosNegocioController.cs` | `Editar` | `Actualizar` |
| `SociosNegocioController.cs` | `Eliminar` | `Eliminar` |
| `SociosNegocioController.cs` | `CrearDireccion` | `Agregar` |
| `SociosNegocioController.cs` | `EditarDireccion` | `Actualizar` |
| `SociosNegocioController.cs` | `EliminarDireccion` | `Eliminar` |

- [ ] **Step 2: Build**

```powershell
cd C:\Users\migue\source\repos\angelm0508\Web
dotnet build Web.slnx -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\webbuild\"
```
Esperado: `0` Errores. Cierra la Fase 5 — todos los controllers MVC de la Web (menos `Cuenta`,
`Home` y `Precios`) quedan protegidos.

- [ ] **Step 3: Commit**

```bash
git add Web.UI/Controllers/DireccionesController.cs Web.UI/Controllers/GruposSnController.cs Web.UI/Controllers/ListadosPrecioController.cs Web.UI/Controllers/NumeracionDocumentoController.cs Web.UI/Controllers/SociosNegocioController.cs
git commit -m "feat(permisos): decora los controllers Web de Socios de Negocio y Documentos con RequierePermiso

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Fase 6 — Web: ocultar botones según permiso

Las Fases 2-5 ya bloquean todo en el servidor (API y MVC) — esta fase es sobre UX: que no se vea
un botón que al usarlo va a fallar con 403/redirect a "Acceso denegado". El patrón es el mismo en
los ~20 módulos; se da un ejemplo completo sobre `FacturasCompra` (módulo con la forma más
completa: grid de detalle + cancelar documento) y una tabla con el resto. **Precondición
verificable antes de tocar cada archivo:** el patrón asume que el botón "Nuevo"/"Agregar línea"
tiene exactamente los `id` mostrados en el ejemplo — confirmar con
`grep -n "id=\"btnNuevo\"\|id=\"btnNuevaLinea\"\|id=\"btnGuardar" <archivo>` antes de editar; si
un módulo difiere (algunos no tienen grid de detalle, p. ej. `Almacenes`/`Fabricantes`/
`GruposArticulo`/`UnidadesMedida`/`ListadosPrecio`/`GruposSn` solo tienen Index+modal simple, sin
`_Form.cshtml` con grid), aplicar solo las partes del patrón que correspondan a lo que ese módulo
realmente tiene (Index: botón "Nuevo"; modal/formulario: botón "Guardar"; sin grid de detalle →
no hay botón de línea que ocultar ni columna de acciones de detalle que gatear).

### Task 20: Ocultar botones — Ventas y Compras (7 módulos, con grid de detalle)

**Files:**
- Modify: `Web.UI/Views/FacturasCompra/Index.cshtml`, `_Form.cshtml`
- Modify: `Web.UI/wwwroot/js/facturascompra.js`
- Modify: los mismos 3 archivos (`Index.cshtml`, `_Form.cshtml`, `<modulo>.js`) de `Cotizaciones`,
  `Pedidos`, `Entregas`, `Facturas`, `PedidosCompra`, `EntregasCompra`

**Interfaces:**
- Consumes: `ClaimsPrincipalExtensions.TienePermiso` (Task 11, disponible en toda vista Razor vía
  `User.TienePermiso(...)`).

- [ ] **Step 1: Ejemplo completo — `FacturasCompra`**

`Views/FacturasCompra/Index.cshtml` — envolver el botón "Nuevo" y agregar el bloque JSON de
permisos (mismo patrón que `datosSeriesFacturaCompra`, agregado junto al `<table>`):

```html
@if (User.TienePermiso("FacturasCompra", Accion.Agregar))
{
    <button type="button" class="btn btn-primary" id="btnNuevo">
        <i class="fa-solid fa-plus me-1"></i>Nuevo
    </button>
}
```

(reemplaza el `<button ... id="btnNuevo">` existente, que hoy no tiene `@if` alrededor.) Y antes
de `@section Scripts`:

```html
<script id="datosPermisosFacturasCompra" type="application/json">
    { "actualizar": @(User.TienePermiso("FacturasCompra", Accion.Actualizar) ? "true" : "false"),
      "eliminar": @(User.TienePermiso("FacturasCompra", Accion.Eliminar) ? "true" : "false") }
</script>
```

`Views/FacturasCompra/_Form.cshtml` — envolver "Agregar línea" (ya está dentro de
`@if (!esEdicion)`, se anida un segundo `@if`), "Cancelar documento" (usa el permiso
`Actualizar`, porque llama a la misma acción `Editar` que el resto de ediciones) y "Guardar":

```html
@if (!esEdicion && User.TienePermiso("FacturasCompra", Accion.Agregar))
{
    <button type="button" class="btn btn-sm btn-outline-primary" id="btnNuevaLinea">
        <i class="fa-solid fa-plus me-1"></i>Agregar línea
    </button>
}
```

```html
@if (esEdicion && (Model.Cancelado ?? "N") != "S" && User.TienePermiso("FacturasCompra", Accion.Actualizar))
{
    <button type="button" class="btn btn-outline-danger" id="btnCancelarDocFacturaCompra" data-entry="@ViewBag.EntryActual">
        <i class="fa-solid fa-ban me-1"></i>Cancelar documento
    </button>
}
@{
    var puedeGuardar = esEdicion ? User.TienePermiso("FacturasCompra", Accion.Actualizar) : User.TienePermiso("FacturasCompra", Accion.Agregar);
}
@if ((Model.Cancelado ?? "N") != "S" && puedeGuardar)
{
    <button type="button" class="btn btn-primary" id="btnGuardarFacturaCompra" data-edicion="@esEdicion.ToString().ToLower()" data-entry="@ViewBag.EntryActual">
        <i class="fa-solid fa-floppy-disk me-1"></i>Guardar
    </button>
}
```

`wwwroot/js/facturascompra.js` — en la función que arma las `columns` del `DataTable` (la celda
de acciones por fila), leer el bloque JSON y condicionar los botones de editar/eliminar:

```javascript
const datosPermisosEl = document.getElementById('datosPermisosFacturasCompra');
const permisos = datosPermisosEl ? JSON.parse(datosPermisosEl.textContent) : { actualizar: true, eliminar: true };
```

(agregar esta constante al tope del `$(function () { ... })`, antes de `$('#tblFacturasCompra').DataTable(...)`) y cambiar el `render` de la columna de acciones:

```javascript
render: entry => `
    ${permisos.actualizar ? `<button class="btn btn-sm btn-outline-primary btn-editar" data-entry="${entry}"><i class="fa-solid fa-pen"></i></button>` : ''}
    ${permisos.eliminar ? `<button class="btn btn-sm btn-outline-danger btn-eliminar" data-entry="${entry}"><i class="fa-solid fa-trash"></i></button>` : ''}
`
```

- [ ] **Step 2: Aplicar la misma transformación a los 6 módulos restantes**

| Módulo | Archivos | CodigoModulo |
|---|---|---|
| Cotizaciones | `Views/Cotizaciones/Index.cshtml`, `_Form.cshtml`, `cotizaciones.js` | `Cotizaciones` |
| Pedidos | `Views/Pedidos/Index.cshtml`, `_Form.cshtml`, `pedidos.js` | `Pedidos` |
| Entregas | `Views/Entregas/Index.cshtml`, `_Form.cshtml`, `entregas.js` | `Entregas` |
| Facturas | `Views/Facturas/Index.cshtml`, `_Form.cshtml`, `facturas.js` | `Facturas` |
| PedidosCompra | `Views/PedidosCompra/Index.cshtml`, `_Form.cshtml`, `pedidoscompra.js` | `PedidosCompra` |
| EntregasCompra | `Views/EntregasCompra/Index.cshtml`, `_Form.cshtml`, `entregascompra.js` | `EntregasCompra` |

Mismo recorte: `btnNuevo` (Index), `btnNuevaLinea` + `btnGuardar<Modulo>` + el botón de cancelar
documento si el módulo lo tiene (`Cotizaciones`/`Pedidos`/`Entregas`/`PedidosCompra`/
`EntregasCompra` no tienen botón de cancelar documento propio — verificar con
`grep -n "btnCancelar" <_Form.cshtml>` antes de asumir que existe; si no existe, omitir esa
parte), bloque `datosPermisos<Modulo>` + lectura en el `.js` con el nombre de `id` correspondiente
a cada módulo. Sustituir `"FacturasCompra"` por el `CodigoModulo` de la fila en cada caso.

- [ ] **Step 3: Build**

```powershell
cd C:\Users\migue\source\repos\angelm0508\Web
dotnet build Web.slnx -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\webbuild\"
```

- [ ] **Step 4: Commit**

```bash
git add Web.UI/Views/FacturasCompra/Index.cshtml Web.UI/Views/FacturasCompra/_Form.cshtml Web.UI/wwwroot/js/facturascompra.js Web.UI/Views/Cotizaciones Web.UI/Views/Pedidos Web.UI/Views/Entregas Web.UI/Views/Facturas Web.UI/Views/PedidosCompra Web.UI/Views/EntregasCompra Web.UI/wwwroot/js/cotizaciones.js Web.UI/wwwroot/js/pedidos.js Web.UI/wwwroot/js/entregas.js Web.UI/wwwroot/js/facturas.js Web.UI/wwwroot/js/pedidoscompra.js Web.UI/wwwroot/js/entregascompra.js
git commit -m "feat(permisos): oculta botones segun permiso en los modulos de Ventas y Compras

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 21: Ocultar botones — catálogos simples (7 módulos, Index + modal, sin grid de detalle)

**Files:** `Index.cshtml` + `<modulo>.js` de `Almacenes`, `Articulos`, `Fabricantes`,
`GruposArticulo`, `UnidadesMedida`, `ListadosPrecio`, `GruposSn` (sin `_Form.cshtml` propio — el
formulario de estos módulos vive dentro del mismo modal simple, no hay grid de detalle que
gatear).

**Interfaces:**
- Consumes: `ClaimsPrincipalExtensions.TienePermiso` (Task 11).

- [ ] **Step 1: Ejemplo completo — `Almacenes`**

`Views/Almacenes/Index.cshtml` — envolver el botón "Nuevo" (verificar primero con
`grep -n "id=\"btnNuevo\"" Views/Almacenes/Index.cshtml` el texto exacto del botón en este
archivo, y envolverlo igual que en Task 20 Step 1) y agregar el bloque de permisos:

```html
@if (User.TienePermiso("Almacenes", Accion.Agregar))
{
    <button type="button" class="btn btn-primary" id="btnNuevo">
        <i class="fa-solid fa-plus me-1"></i>Nuevo
    </button>
}

<script id="datosPermisosAlmacenes" type="application/json">
    { "actualizar": @(User.TienePermiso("Almacenes", Accion.Actualizar) ? "true" : "false"),
      "eliminar": @(User.TienePermiso("Almacenes", Accion.Eliminar) ? "true" : "false") }
</script>
```

`wwwroot/js/almacenes.js` — misma lectura del bloque JSON y el mismo condicional en el `render`
de la columna de acciones que en Task 20 Step 1 (sustituyendo el id del bloque JSON por
`datosPermisosAlmacenes`).

- [ ] **Step 2: Aplicar la misma transformación al resto**

| Módulo | Archivos | CodigoModulo |
|---|---|---|
| Articulos | `Views/Articulos/Index.cshtml`, `articulos.js` | `Articulos` |
| Fabricantes | `Views/Fabricantes/Index.cshtml`, `fabricantes.js` | `Fabricantes` |
| GruposArticulo | `Views/GruposArticulo/Index.cshtml`, `gruposarticulo.js` (verificar el nombre real del archivo `.js` con `ls wwwroot/js/ \| grep -i grupoarticulo` — puede diferir en mayúsculas/separación) | `GruposArticulo` |
| UnidadesMedida | `Views/UnidadesMedida/Index.cshtml`, `unidadesmedida.js` | `UnidadesMedida` |
| ListadosPrecio | `Views/ListadosPrecio/Index.cshtml`, `listadosprecio.js` | `ListadosPrecio` |
| GruposSn | `Views/GruposSn/Clientes.cshtml` y `Proveedores.cshtml` (dos vistas, mismo módulo), `gripossn.js` (verificar nombre real) | `GruposSn` |

(Verificar el nombre exacto de cada archivo `.js` con `ls Web.UI/wwwroot/js/` antes de editar —
no todos siguen necesariamente el mismo casing que el nombre del controller.)

- [ ] **Step 3: Build**

```powershell
cd C:\Users\migue\source\repos\angelm0508\Web
dotnet build Web.slnx -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\webbuild\"
```

- [ ] **Step 4: Commit**

```bash
git add Web.UI/Views/Almacenes Web.UI/Views/Articulos Web.UI/Views/Fabricantes Web.UI/Views/GruposArticulo Web.UI/Views/UnidadesMedida Web.UI/Views/ListadosPrecio Web.UI/Views/GruposSn Web.UI/wwwroot/js/almacenes.js Web.UI/wwwroot/js/articulos.js Web.UI/wwwroot/js/fabricantes.js
git commit -m "feat(permisos): oculta botones segun permiso en catalogos simples de Inventario y Socios

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

(Completar el `git add` con los nombres reales de los `.js` de `GruposArticulo`/`UnidadesMedida`/
`ListadosPrecio`/`GruposSn` confirmados en el Step 2.)

---

### Task 22: Ocultar botones — módulos con grid de detalle restantes (EntradasMercancia, SalidasMercancia, GruposUnidadMedida, NumeracionDocumento, SociosNegocio/Direcciones)

**Files:** `Index.cshtml` + `_Form.cshtml` (donde exista) + `.js` de cada uno de los 5 módulos.
`Existencias` **no** lleva cambios en esta tarea — es de solo lectura, no tiene botones de
crear/editar/eliminar que ocultar (confirmado en Task 18: solo `Index`/`ObtenerTodos`/
`BuscarArticulos`/`Kardex`, los 4 son `Ver`).

**Interfaces:**
- Consumes: `ClaimsPrincipalExtensions.TienePermiso` (Task 11); mismo patrón de
  `datosPermisos<Modulo>` + `@if` de Task 20.

- [ ] **Step 1: `EntradasMercancia`, `SalidasMercancia`, `GruposUnidadMedida`,
  `NumeracionDocumento`**

Mismo patrón exacto que Task 20 (botón "Nuevo" en `Index.cshtml`, bloque `datosPermisos<Modulo>`,
botón "Agregar línea" + "Guardar" en `_Form.cshtml`, columna de acciones gateada en el `.js`),
con `CodigoModulo` = `EntradasMercancia`, `SalidasMercancia`, `GruposUnidadMedida` y
`NumeracionDocumento` respectivamente. Dos diferencias a verificar con `grep` antes de editar
cada archivo:
- `NumeracionDocumentoController` no tiene acción `Crear`/`FormularioCrear` (las series no se dan
  de alta desde la Web, solo se editan — ver Task 19) → `Views/NumeracionDocumento/Index.cshtml`
  no tiene botón "Nuevo" que ocultar; solo gatear la columna de acciones (editar) de la tabla
  principal y el botón "Agregar línea"/acciones de línea dentro del detalle de cada serie.
- `GruposUnidadMedida` no tiene botón de "cancelar documento" (no es un documento comercial) — no
  aplica esa parte del patrón de Task 20.

- [ ] **Step 2: `SociosNegocio` (y la pantalla `Direcciones`)**

`Views/SociosNegocio/Index.cshtml` — envolver "Nuevo" con `Accion.Agregar` del módulo
`SociosNegocio`, bloque `datosPermisosSociosNegocio` con `actualizar`/`eliminar`, gatear la
columna de acciones en `sociosnegocio.js` (verificar nombre real del archivo).

Dentro del formulario de socio (verificar el archivo real — puede ser `_Form.cshtml` o estar
embebido en `Crear.cshtml`/`FormularioEditar.cshtml` según cómo esté armado hoy, confirmar con
`ls Web.UI/Views/SociosNegocio/`), los botones "Agregar dirección"/editar/eliminar dirección
(`CrearDireccion`/`EditarDireccion`/`EliminarDireccion`) se gatean con `Accion.Agregar`/
`Actualizar`/`Eliminar` del mismo módulo `SociosNegocio`.

`Views/Direcciones/Index.cshtml` + `.js` — mismo patrón que Task 21 (catálogo simple, sin grid
anidado), `CodigoModulo` = `SociosNegocio` (comparte módulo con el socio, no es uno propio — ver
Task 10/19).

- [ ] **Step 3: Build**

```powershell
cd C:\Users\migue\source\repos\angelm0508\Web
dotnet build Web.slnx -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\webbuild\"
```
Esperado: `0` Errores. Cierra la Fase 6.

- [ ] **Step 4: Commit**

```bash
git add Web.UI/Views/EntradasMercancia Web.UI/Views/SalidasMercancia Web.UI/Views/GruposUnidadMedida Web.UI/Views/NumeracionDocumento Web.UI/Views/SociosNegocio Web.UI/Views/Direcciones Web.UI/wwwroot/js/entradasmercancia.js Web.UI/wwwroot/js/salidasmercancia.js Web.UI/wwwroot/js/gruposunidadmedida.js Web.UI/wwwroot/js/numeraciondocumento.js Web.UI/wwwroot/js/sociosnegocio.js Web.UI/wwwroot/js/direcciones.js
git commit -m "feat(permisos): oculta botones segun permiso en los modulos restantes con detalle

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

(Confirmar los nombres reales de cada `.js` con `ls Web.UI/wwwroot/js/` antes del `git add` — se
usó el nombre más probable según el casing del resto del proyecto, no verificado archivo por
archivo para esta tarea.)

---

## Fase 7 — Verificación conjunta

### Task 23: Build + suite completa + checklist manual

**Files:** ninguno (task de verificación).

- [ ] **Step 1: Build y suite completa de la API**

```powershell
cd C:\Users\migue\source\repos\angelm0508\API
dotnet build API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apibuild\"
dotnet test API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apitest\"
```
Esperado: `0` Errores; suite completa en verde (baseline del inicio de este plan + todos los
tests agregados en Tasks 1-6).

- [ ] **Step 2: Build de la Web**

```powershell
cd C:\Users\migue\source\repos\angelm0508\Web
dotnet build Web.slnx -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\webbuild\"
```
Esperado: `0` Errores, `0` Advertencias nuevas.

- [ ] **Step 3: Seed de un usuario de prueba y una serie de permisos restringida**

Contra `API_DB_TEST` (requiere que ya exista al menos un usuario no-`SuperUsuario` para probar —
si no hay ninguno, usar `POST api/Usuario` con `SuperUsuario: "N"` para crear uno de prueba):

```sql
-- Verificar que el usuario de prueba existe y no es SuperUsuario
SELECT ID, Codigo, SuperUsuario FROM Usuario WHERE SuperUsuario = 'N';
```

- [ ] **Step 4: Checklist manual (navegador, API + Web corriendo contra `API_DB_TEST`)**

1. Iniciar sesión como un usuario no-`SuperUsuario` que todavía no tiene fila en
   `UsuarioPermiso` → ve el menú completo, igual que hoy (acceso total por defecto).
2. Iniciar sesión como `SuperUsuario` → ir a Administración → Usuarios → elegir ese usuario de
   prueba → Permisos → dejarle marcado solo `Ver` en `Almacenes` y guardar.
3. Cerrar sesión, volver a entrar como el usuario de prueba → el módulo `Almacenes` sigue
   visible en el menú, pero dentro de la pantalla no aparece el botón "Nuevo" ni los botones de
   editar/eliminar de la tabla.
4. Con ese mismo usuario, navegar directo a `/EntradasMercancia` (un módulo al que no le quedó
   ningún permiso) → la app redirige a "Acceso denegado" (no al login) y el módulo no aparece en
   el menú.
5. Intentar, con herramientas de red (`curl`/Postman) usando el JWT de ese usuario, un
   `POST api/Almacen` (crear) → la API responde `403` con el mensaje "No tenés permiso para
   realizar esta acción."
6. Como `SuperUsuario`, crear una plantilla "Solo lectura" con `Ver` marcado en 2-3 módulos y
   nada más, aplicarla al usuario de prueba desde `Usuarios/Permisos` → la matriz del usuario
   queda igual a la plantilla; editar después la plantilla no cambia lo que ya tiene el usuario
   (y viceversa).
7. `SuperUsuario` sigue viendo y pudiendo todo en todo momento, sin importar lo que tenga o no
   tenga marcado en su propia fila de `UsuarioPermiso` (no debería tener ninguna).
8. Un usuario no-`SuperUsuario` intentando entrar a `/Usuarios` o `/Plantillas` directo por URL →
   "Acceso denegado" (nunca tiene el módulo `Usuarios` asignado).

- [ ] **Step 5: Confirmar que no queda nada sin commitear salvo `appsettings.json`**

```powershell
cd C:\Users\migue\source\repos\angelm0508\API
git status --porcelain
cd C:\Users\migue\source\repos\angelm0508\Web
git status --porcelain
```
Esperado: API muestra únicamente ` M API.Service.WebApi/appsettings.json`; Web, árbol limpio.

- [ ] **Step 6: Reporte final**

No hay commit en este task — es de verificación. Usar
`superpowers:finishing-a-development-branch` para presentar las opciones de integración al
usuario una vez completadas las 22 tareas anteriores.

## Riesgos y notas de cierre

- Las Fases 6 (ocultar botones) dependen de que cada implementer **lea primero** el archivo real
  antes de aplicar el patrón — a diferencia de las Fases 2 y 5 (decorar controllers), donde la
  tabla ya trae el nombre exacto de cada método relevado de los archivos actuales, en la Fase 6
  los nombres de archivo `.js` y la forma exacta de cada `_Form.cshtml`/`Index.cshtml` no se
  relevaron uno por uno (sería releer ~40 archivos completos solo para este plan) — el patrón y
  el ejemplo trabajado (Task 20 Step 1) son el contrato; cualquier diferencia real encontrada al
  leer un archivo se resuelve con el mismo criterio ("ocultar lo que exista, con el permiso que
  corresponda a la acción real que ese botón dispara"), sin inventar botones que no existan.
- Cualquier endpoint nuevo agregado por un plan en curso en paralelo (ej. `Abiertos` de
  `2026-09-13-documentos-base-compras-implementation.md`, si avanza mientras este plan se
  ejecuta) no queda cubierto automáticamente — hay que decorarlo a mano con
  `[RequierePermiso]` siguiendo el mismo criterio (ver nota en Task 8).
- `Departamento`, `Impuesto`, `Moneda`, `Municipio`, `Pais` quedan fuera de la matriz de permisos
  (catálogos de referencia sin pantalla propia en la Web, spec §Contexto) — siguen protegidos
  solo por `[Authorize]`, como hoy.
