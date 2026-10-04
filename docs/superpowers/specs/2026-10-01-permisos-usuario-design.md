# Administración de permisos de usuario — Diseño

**Fecha:** 2026-10-01
**Repos:** `angelm0508/API` (.NET 7, N-capas, rama `desarrollo`), `angelm0508/Web` (.NET 8 MVC, rama `main`)

## Objetivo

Cada usuario debe tener, por módulo (Compras, Ventas, Numeración de documentos, Socios de
Negocio, etc.), permiso para **Ver**, **Agregar**, **Actualizar** y **Eliminar**. Sin `Ver` en un
módulo, ese módulo no aparece en el menú de la Web ni se puede acceder por URL directa. Sin
`Agregar`/`Actualizar`/`Eliminar`, los botones correspondientes no aparecen y el endpoint de la
API los rechaza igual aunque se invoquen directo.

Sin roles. Asignación directa por usuario, más plantillas reutilizables que se **copian** al
aplicarse a un usuario (no quedan vinculadas — editar la plantilla después no afecta a quien ya
la aplicó, y editar el usuario después no afecta la plantilla).

### Decisiones tomadas (brainstorming)

1. **Modelo de datos:** una sola matriz `(sujeto, módulo) → 4 booleanos`, no dos conceptos
   separados de "módulo asignado" + "acciones permitidas" — `Ver` marcado ES la asignación del
   módulo.
2. **Plantillas:** copian su matriz al usuario al aplicarse (snapshot editable, sin vínculo vivo).
3. **Usuarios sin permisos asignados:** *deny by default* — sin filas en `UsuarioPermiso` el
   usuario no tiene acceso a ningún módulo ni acción (solo `SuperUsuario` accede a todo).
   *(Enmienda 2026-10-03: la decisión original era "acceso total por defecto" para no bloquear a los
   usuarios existentes al desplegar; se invirtió porque un usuario recién creado sin permisos podía
   operar todo el sistema. Los usuarios no-superusuario ya existentes necesitan que un admin les
   asigne permisos o una plantilla.)*
4. **Alcance de la UI:** bloqueo real en la API (403 si falta el permiso) **y** ocultar/deshabilitar
   los botones correspondientes en la Web — no solo uno de los dos.
5. **Gestión de permisos restringida a `SuperUsuario`** (campo ya existente en `Usuario`).

## Contexto verificado

### Estado actual de autenticación

- **API:** `TokenService.GenerarToken(Usuario)` arma el JWT con 3 claims: `ClaimTypes.NameIdentifier`
  (Id), `ClaimTypes.Name` (Codigo), y `"SuperUsuario"` (`"S"`/`"N"`, ya existe). `Usuario.SuperUsuario`
  es `string?` en la entidad y columna de BD.
- **Web:** `CuentaController.Login` llama a `IAuthApiClient.LoginAsync`, recibe `LoginResponseDTO
  { Token, UsuarioNombre, ExpirasEn }`, y arma la cookie de sesión (`AuthConstants.EsquemaCookie`)
  con **solo** `ClaimTypes.Name` y `AuthConstants.ClaimJwtToken` (el JWT crudo) — **no copia
  `SuperUsuario` ni ningún otro claim del JWT a la cookie hoy**. `JwtAuthorizationHandler` adjunta
  ese JWT crudo como `Authorization: Bearer` en cada request saliente hacia la API, así que
  cualquier enforcement en la API ve siempre el JWT real y vigente.
- **`Program.cs`:** cookie `ExpireTimeSpan = 60 min`, `SlidingExpiration = true`,
  `AccessDeniedPath = "/Cuenta/Login"` (hoy no distingue "no autenticado" de "sin permiso" — se
  ajusta, ver §3).
- **`Startup.cs` (API):** `AddAuthorization()` sin políticas configuradas — sin infraestructura de
  permisos previa.

### Módulos existentes (de `Web/Web.UI/Views/Shared/_Layout.cshtml`)

| Grupo | Módulos (= nombre del controller, Web y API) |
|---|---|
| Inventario | `Articulos`, `Fabricantes`, `GruposArticulo`, `UnidadesMedida`, `GruposUnidadMedida`, `Almacenes`, `Existencias`, `EntradasMercancia`, `SalidasMercancia` |
| Socios de Negocio | `SociosNegocio`, `GruposSn`, `ListadosPrecio` |
| Documentos | `NumeracionDocumento` |
| Ventas | `Cotizaciones`, `Pedidos`, `Entregas`, `Facturas` |
| Compras | `PedidosCompra`, `EntregasCompra`, `FacturasCompra` |
| Administración | `Usuarios` (nuevo — no existe hoy pantalla de usuarios en la Web; se crea una de solo lectura, ver §4) |

21 módulos en total. `GET api/Usuario` (`ObtenerTodoAsync`) ya existe y alcanza para listar
usuarios en la pantalla nueva — no se agrega alta/edición de usuario (fuera de alcance, no se
pidió).

## §1 — Modelo de datos (API)

Tres tablas nuevas, sin tocar ninguna existente. Script DDL en `API/sql/2026-10-01-permisos-usuario.sql`
(patrón ya usado en el proyecto para scripts de una sola vez), idempotente con `IF NOT EXISTS`.

```sql
CREATE TABLE Modulo (
    Codigo nvarchar(40) NOT NULL PRIMARY KEY,
    Nombre nvarchar(100) NOT NULL,
    Grupo nvarchar(40) NOT NULL
);

CREATE TABLE UsuarioPermiso (
    UsuarioId int NOT NULL,
    CodigoModulo nvarchar(40) NOT NULL,
    Ver bit NOT NULL DEFAULT 0,
    Agregar bit NOT NULL DEFAULT 0,
    Actualizar bit NOT NULL DEFAULT 0,
    Eliminar bit NOT NULL DEFAULT 0,
    PRIMARY KEY (UsuarioId, CodigoModulo),
    CONSTRAINT fk_usuariopermiso_usuario FOREIGN KEY (UsuarioId) REFERENCES Usuario(ID),
    CONSTRAINT fk_usuariopermiso_modulo FOREIGN KEY (CodigoModulo) REFERENCES Modulo(Codigo)
);

CREATE TABLE Plantilla (
    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Nombre nvarchar(100) NOT NULL
);

CREATE TABLE PlantillaPermisoDetalle (
    PlantillaId int NOT NULL,
    CodigoModulo nvarchar(40) NOT NULL,
    Ver bit NOT NULL DEFAULT 0,
    Agregar bit NOT NULL DEFAULT 0,
    Actualizar bit NOT NULL DEFAULT 0,
    Eliminar bit NOT NULL DEFAULT 0,
    PRIMARY KEY (PlantillaId, CodigoModulo),
    CONSTRAINT fk_plantillapermiso_plantilla FOREIGN KEY (PlantillaId) REFERENCES Plantilla(Id),
    CONSTRAINT fk_plantillapermiso_modulo FOREIGN KEY (CodigoModulo) REFERENCES Modulo(Codigo)
);

-- Seed: un Modulo por cada controller existente + Usuarios.
INSERT INTO Modulo (Codigo, Nombre, Grupo) VALUES
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
('Usuarios', 'Usuarios', 'Administracion');
```

**Entidades EF Core** (`API.Domain.Entity/Models/Modulo.cs`, `UsuarioPermiso.cs`, `Plantilla.cs`,
`PlantillaPermisoDetalle.cs`) + bloques `OnModelCreating` en `ApiDbTestContext.cs` — mismo patrón
que cualquier entidad existente del proyecto (clave compuesta vía `HasKey` con tupla, igual que
`FacturaDetalle`).

**Semántica de "sin fila = sin acceso" (decisión 3, enmendada):** un usuario sin ninguna fila en
`UsuarioPermiso` se trata como si tuviera **todo en `false`** en todos los módulos (el token no
lleva ningún claim `perm`). Solo `SuperUsuario` se salta la matriz.

## §2 — Resolución de permisos y propagación (API)

### Dominio: `IPermisoDomain` / `PermisoDomain`

```csharp
public enum Accion { Ver, Agregar, Actualizar, Eliminar }

public record PermisoModulo(string CodigoModulo, bool Ver, bool Agregar, bool Actualizar, bool Eliminar);

public interface IPermisoDomain
{
    // Si el usuario no tiene ninguna fila, devuelve una lista vacia -- la interpretacion de
    // "lista vacia = sin acceso" vive en el llamador (TokenService), no aca.
    Task<IReadOnlyList<PermisoModulo>> ObtenerMatrizUsuarioAsync(int usuarioId);
    Task GuardarMatrizUsuarioAsync(int usuarioId, IReadOnlyList<PermisoModulo> matriz);
    Task<IReadOnlyList<PermisoModulo>> ObtenerMatrizPlantillaAsync(int plantillaId);
    Task GuardarMatrizPlantillaAsync(int plantillaId, IReadOnlyList<PermisoModulo> matriz);
    Task AplicarPlantillaAsync(int usuarioId, int plantillaId); // borra UsuarioPermiso del usuario, copia los de la plantilla
}
```

`GuardarMatrizUsuarioAsync`/`GuardarMatrizPlantillaAsync` hacen reemplazo total (borran todas las
filas del sujeto y insertan las del parámetro que tengan al menos un booleano en `true` — una fila
con las 4 en `false` no se guarda, equivale a no tener fila).

### Claims del JWT (`TokenService.GenerarToken`)

```csharp
var claims = new List<Claim>
{
    new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
    new(ClaimTypes.Name, usuario.Codigo),
    new("SuperUsuario", usuario.SuperUsuario ?? "N")
};

if (usuario.SuperUsuario != "S")
{
    var matriz = await _permisoDomain.ObtenerMatrizUsuarioAsync(usuario.Id);
    // Sin filas = sin acceso (decision 3 enmendada): no se emite ningun claim "perm".
    foreach (var m in matriz)
    {
        var letras = (m.Ver ? "V" : "") + (m.Agregar ? "A" : "") + (m.Actualizar ? "U" : "") + (m.Eliminar ? "E" : "");
        if (letras.Length > 0) claims.Add(new Claim("perm", $"{m.CodigoModulo}:{letras}"));
    }
}
// SuperUsuario == "S": no se emite ningun claim "perm" -- el bypass en RequierePermisoAttribute
// se resuelve solo con el claim SuperUsuario, sin necesitar enumerar modulos.
```

`TokenService` pasa a depender de `IPermisoDomain` (inyectado por constructor; el registro
`AddTransient<ITokenService, TokenService>` no cambia).

### `RequierePermisoAttribute` (API) — `API.Service.WebApi/Authorization/RequierePermisoAttribute.cs`

```csharp
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

        var letra = _accion switch { Accion.Ver => 'V', Accion.Agregar => 'A', Accion.Actualizar => 'U', Accion.Eliminar => 'E', _ => ' ' };
        var tienePermiso = user.FindAll("perm")
            .Select(c => c.Value.Split(':'))
            .Where(p => p.Length == 2 && p[0] == _codigoModulo)
            .Any(p => p[1].Contains(letra));

        if (!tienePermiso)
        {
            context.Result = new ObjectResult(new Respuesta<object> { Mensaje = "No tenés permiso para realizar esta acción." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
```

Se aplica en **cada acción** de los 20 controllers de documento/catálogo existentes (no en
`AuthController`, que es `[AllowAnonymous]`), ejemplo sobre `PedidoCompraController`:

```csharp
[HttpGet("{id:int}")]
[RequierePermiso("PedidosCompra", Accion.Ver)]
public async Task<ActionResult<Respuesta<PedidoCompraDTO>>> Obtener(...)

[HttpPost]
[RequierePermiso("PedidosCompra", Accion.Agregar)]
public async Task<ActionResult<Respuesta<int>>> InsertarAsync(...)

[HttpPut("{id:int}")]
[RequierePermiso("PedidosCompra", Accion.Actualizar)]
public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync(...)

[HttpDelete("{id:int}")]
[RequierePermiso("PedidosCompra", Accion.Eliminar)]
public async Task<ActionResult<Respuesta<bool>>> EliminarAsync(...)
```

Las acciones de "listar"/"buscar" (`ObtenerTodoAsync`, `Abiertos`, `BuscarXxx`) llevan
`Accion.Ver`. El `CodigoModulo` de cada controller es exactamente el nombre usado en la tabla de
módulos de §Contexto (idéntico al nombre del controller sin el sufijo `Controller`).

**Controllers `...Detalle` (ej. `FacturaCompraDetalleController`, `PedidoDetalleController`,
`EntradaMercanciaDetalleController`):** no son un módulo aparte — usan el `CodigoModulo` del
documento padre (`FacturaCompraDetalleController` → `"FacturasCompra"`, igual que
`FacturaCompraController`). Sus acciones de edición en vivo (`CrearLinea` → `Agregar`,
`EditarLinea`/`ActualizarAsync` → `Actualizar`, `EliminarAsync` → `Eliminar`,
`ObtenerDetalle`/`ObtenerPorXxxAsync` → `Ver`) quedan protegidas por el mismo permiso que el resto
del documento. Esto suma bastantes más archivos que los 20 controllers de documento/catálogo
contados en §Contexto — el plan de implementación enumera la lista completa y exacta de
controllers (documento + detalle) a tocar en ambos repos.

### Endpoints de administración

- `API.Service.WebApi/Controllers/ModuloController.cs` — `GET api/Modulo` (catálogo completo, sin
  parámetros), `[RequierePermiso("Usuarios", Accion.Ver)]`.
- `API.Service.WebApi/Controllers/UsuarioPermisoController.cs` — `GET api/UsuarioPermiso/{usuarioId}`
  (`Accion.Ver`), `PUT api/UsuarioPermiso/{usuarioId}` (reemplazo total, `Accion.Actualizar`),
  `POST api/UsuarioPermiso/{usuarioId}/AplicarPlantilla/{plantillaId}` (`Accion.Actualizar`).
- `API.Service.WebApi/Controllers/PlantillaController.cs` — CRUD completo de `Plantilla` (`Ver`/
  `Agregar`/`Actualizar`/`Eliminar`), más `GET/PUT api/Plantilla/{id}/Permisos` (misma forma que
  `UsuarioPermiso`).

Los 3 controllers exigen `"Usuarios"` como módulo — en la práctica solo `SuperUsuario` tiene ese
módulo asignado (nunca se le asigna a nadie más desde la UI, ver §4), así que el filtro ya
alcanza; no se agrega un segundo chequeo "es SuperUsuario" redundante, salvo en el propio
`UsuarioPermisoController`/`PlantillaController`, donde SÍ conviene reforzar con
`if (user.FindFirst("SuperUsuario")?.Value != "S") return Forbid();` al principio de cada acción
de escritura, como defensa en profundidad ante el caso borde de que alguien manipule
`UsuarioPermiso` directo en la base y se autoasigne el módulo `Usuarios`.

### DTOs

```csharp
// API.Application.DTO/permiso/PermisoModuloDTO.cs
public class PermisoModuloDTO
{
    public string CodigoModulo { get; set; } = null!;
    public string NombreModulo { get; set; } = null!; // para pintar la grilla sin una 2da llamada
    public string Grupo { get; set; } = null!;
    public bool Ver { get; set; }
    public bool Agregar { get; set; }
    public bool Actualizar { get; set; }
    public bool Eliminar { get; set; }
}
// API.Application.DTO/permiso/ModuloDTO.cs: { Codigo, Nombre, Grupo }
// API.Application.DTO/permiso/PlantillaDTO.cs / PlantillaCrearDTO.cs / PlantillaActualizarDTO.cs: { Id, Nombre }
```

## §3 — Web: propagación, enforcement y menú

### `CuentaController.Login` — copiar claims del JWT a la cookie

```csharp
var handler = new JwtSecurityTokenHandler();
var jwt = handler.ReadJwtToken(respuesta.Dato.Token); // solo lectura, la API ya validó/firmó esto

var claims = new List<Claim>
{
    new(ClaimTypes.Name, respuesta.Dato.UsuarioNombre ?? modelo.Usuario),
    new(AuthConstants.ClaimJwtToken, respuesta.Dato.Token)
};
claims.AddRange(jwt.Claims.Where(c => c.Type == "SuperUsuario" || c.Type == "perm"));
```

(Agregar `using System.IdentityModel.Tokens.Jwt;` y `using System.Linq;` si no están.)

### `ClaimsPrincipal.TienePermiso` — `Web.UI/Authorization/ClaimsPrincipalExtensions.cs`

```csharp
public static class ClaimsPrincipalExtensions
{
    public static bool TienePermiso(this ClaimsPrincipal user, string codigoModulo, Accion accion)
    {
        if (user.FindFirst("SuperUsuario")?.Value == "S") return true;

        var letra = accion switch { Accion.Ver => 'V', Accion.Agregar => 'A', Accion.Actualizar => 'U', Accion.Eliminar => 'E', _ => ' ' };
        return user.FindAll("perm")
            .Select(c => c.Value.Split(':'))
            .Where(p => p.Length == 2 && p[0] == codigoModulo)
            .Any(p => p[1].Contains(letra));
    }
}
```

(`enum Accion` se duplica en `Web.UI/Authorization/Accion.cs` — mismos 4 valores, mismo criterio
de duplicación ya aceptado en el proyecto entre API y Web.)

### `RequierePermisoAttribute` (Web) — mismo nombre y forma que el de la API, adaptado a MVC:

```csharp
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
```

Se aplica en cada acción de los 20 controllers MVC existentes, mismo mapeo Ver/Agregar/Actualizar/
Eliminar que en la API (ej. `Index`/`ObtenerTodos`/buscadores → `Ver`; `Crear` → `Agregar`;
`Editar`/acciones de línea de edición → `Actualizar`; `Eliminar`/cancelar documento → `Eliminar`).

**`CuentaController` gana una acción `AccesoDenegado`** (vista simple, "No tenés permiso para ver
esta sección") y `Program.cs` cambia `options.AccessDeniedPath = "/Cuenta/Login"` a
`"/Cuenta/AccesoDenegado"` — hoy un usuario autenticado sin permiso caía de vuelta al login, lo
cual confunde (ya está logueado). Esto es una corrección chica, no un nuevo flujo.

### Menú (`_Layout.cshtml`)

Cada `<a class="nav-link" asp-controller="X" ...>` de módulo se envuelve en
`@if (User.TienePermiso("X", Accion.Ver))`. Cada bloque de submenú (Inventario/Socios de
Negocio/Ventas/Compras) se muestra solo si alguno de sus módulos hijos es visible — se calcula una
variable bool adicional junto a las `EsActivoXxx` ya existentes, ej.:
`bool MuestraInventario = new[] { "Articulos", "Fabricantes", ... }.Any(m => User.TienePermiso(m, Accion.Ver));`

### Botones por pantalla

En cada `Index.cshtml` (botón "Nuevo") y `_Form.cshtml` (botones "Guardar"/"Cancelar documento")
de los 20 módulos, se calcula al tope de la vista `@{ var puedeAgregar = User.TienePermiso("X",
Accion.Agregar); }` (y `Actualizar`/`Eliminar` según corresponda) y se envuelve el botón en el
`@if`. El botón "Editar"/"Eliminar" por fila de las tablas (`DataTables`, generado en el `.js` de
cada módulo) también debe respetar el permiso — como esos botones se generan en JavaScript, el
dato de qué puede hacer el usuario logueado viaja una vez al cargar la página vía un bloque
`<script id="datosPermisosX" type="application/json">` (mismo patrón ya usado para
`datosSeriesFacturaCompra` en los módulos de documento) con `{ agregar, actualizar, eliminar }`,
y el `.js` lo lee al armar las columnas de acciones de la tabla.

## §4 — Pantallas nuevas

- **`Web.UI/Controllers/UsuariosController.cs`** + `Views/Usuarios/Index.cshtml`: lista de
  usuarios (`GET api/Usuario`, de solo lectura — sin alta/edición de usuario), columna "Permisos"
  con un botón que abre `Usuarios/Permisos?usuarioId=`.
- **`Views/Usuarios/Permisos.cshtml`** + **`Views/Plantillas/Permisos.cshtml`**: mismo partial
  view `_GrillaPermisos.cshtml` (tabla módulo × 4 checkboxes, agrupada por `Grupo`), parametrizado
  por la URL de `GET`/`PUT` a usar (`/Usuarios/ObtenerPermisos`/`/Usuarios/GuardarPermisos` vs.
  `/Plantillas/ObtenerPermisos`/`/Plantillas/GuardarPermisos`, proxies MVC a los endpoints de
  §2). La vista de usuario agrega un selector "Aplicar plantilla" + botón, que llama al proxy de
  `AplicarPlantilla` y recarga la grilla.
- **`Web.UI/Controllers/PlantillasController.cs`** + `Views/Plantillas/Index.cshtml`: CRUD simple
  de plantillas (solo `Nombre`), cada fila con un botón "Permisos" → `Plantillas/Permisos?plantillaId=`.

Las 3 pantallas y sus controllers requieren `[RequierePermiso("Usuarios", Accion.Ver)]` como
mínimo (y la acción correspondiente en las de escritura) — en la práctica solo accesibles para
`SuperUsuario`, por la misma razón que en la API.

## §5 — Pruebas

### API (unit)

- `RequierePermisoAttribute`: SuperUsuario bypassa sin mirar `perm`; claim `perm` con la letra
  presente permite; sin la letra, o sin claim para ese módulo, devuelve 403 con el mensaje
  esperado.
- `TokenService.GenerarToken`: SuperUsuario → sin claims `perm`; usuario con matriz → un claim
  `perm` por módulo con sus letras exactas, módulos sin nada marcado no emiten claim; usuario sin
  ninguna fila → ningún claim `perm` (sin acceso, *deny by default*).
- `PermisoDomain.GuardarMatrizUsuarioAsync`/`GuardarMatrizPlantillaAsync`: reemplazo total (una
  fila vieja que no viene en la matriz nueva se borra); fila con las 4 en `false` no se inserta.
- `PermisoDomain.AplicarPlantillaAsync`: copia exacta de la matriz de la plantilla al usuario,
  borrando lo que el usuario tuviera antes.
- Controllers nuevos (`ModuloController`, `UsuarioPermisoController`, `PlantillaController`):
  patrón estándar del proyecto (mock de Application, 200/400/404/403 según corresponda).

### Web — checklist manual

Ya descrito en la sección "Testing y rollout" del brainstorming: usuario sin matriz ve todo;
quitarle `Ver` a un módulo lo saca del menú y bloquea la URL directa con la página de acceso
denegado (no el login); dejarle solo `Ver` oculta "Nuevo"/"Editar"/"Eliminar"; crear y aplicar una
plantilla deja al usuario editable aparte sin tocar la plantilla; SuperUsuario nunca pierde nada.

## Riesgos y fuera de alcance

- **Staleness de permisos:** un cambio de permisos no afecta una sesión ya iniciada hasta el
  próximo login (la cookie expira a los 60 min, `SlidingExpiration` la renueva mientras haya
  actividad — en el peor caso alguien sigue con permisos viejos bastante más de una hora si usa el
  sistema sin pausa). Invalidar sesiones activas al cambiar permisos queda fuera de alcance.
- **Sin alta/edición de usuario** en la Web — la pantalla `Usuarios` nueva es de solo lectura
  (lista + botón de permisos), reutilizando el CRUD que ya existe en la API si en el futuro se
  pide esa pantalla.
- **`Usuarios` como módulo autoprotegido:** solo se asigna a `SuperUsuario` desde el flujo normal;
  el refuerzo explícito en `UsuarioPermisoController`/`PlantillaController` (más que confiar solo
  en el filtro) cubre el caso de que alguien lo manipule directo en la base.
- Fuera de alcance: permisos a nivel de fila/dato (ej. "solo ver sus propios socios de negocio");
  auditoría de cambios de permisos; notificar al usuario afectado cuando cambian sus permisos.
