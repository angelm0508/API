# Lista de precio en la creación de documentos — Diseño

**Fecha:** 2026-09-09
**Repos:** `angelm0508/API` (.NET 7, N-capas, rama `desarrollo`), `angelm0508/Web` (.NET 8 MVC, rama `main`)

## Objetivo

Al elegir un artículo en la línea de un documento, colocar el precio **resuelto según la lista de
precio del socio de negocio del documento**, en vez del `PrecioUnitario` base del artículo.

Ejemplo: artículo `0003-0002` con `PrecioUnitario = 22`; lista "Venta" con `Factor = 1.20` y sin
redondeo; documento a un cliente que tiene esa lista asignada → el precio de la línea se coloca
en `22 × 1.20 = 26.40`.

Referencia de comportamiento: SAP Business One — la lista de precio asignada al interlocutor
comercial (`OCRD.ListNum`) determina el precio por defecto de la fila; el usuario puede
sobrescribirlo a mano; al cambiar el interlocutor con filas ya cargadas, el sistema ofrece
recalcular.

Sin cambios de esquema. Sin migración de datos. La lista "Venta" (o las que se necesiten) se
crean con la pantalla existente `Web /ListadosPrecio`.

### Decisiones tomadas (brainstorming)

1. **Cálculo:** factor + cadena `Base` recursiva + redondeo configurable de la lista asignada.
2. **Alcance:** los 7 documentos comerciales — Cotización, Pedido, Entrega, Factura, Pedido de
   compra, Entrega de compra, Factura de compra. (En compra la lista se resuelve desde
   `SocioNegocio.NumLstPrecio` del proveedor; el modelo no distingue por tipo de socio.)
3. **Endpoint:** por artículo + lista — `GET api/Articulo/{codigo}/Precio?listaPrecio={entry}`.
4. **Recálculo:** al cambiar el socio del documento con líneas ya cargadas, recalcular **todas**
   las líneas (con confirmación). El precio queda editable a mano después.

## Contexto verificado

### Datos y entidades

- **`ListadoPrecio`** (`API.Domain.Entity/Models/ListadoPrecio.cs`): `Entry` (PK, no
  autonumérica — el domain calcula `max+1`), `Nombre`, `Base` (`int?` → `ListadoPrecio.Entry`
  de la lista de la que deriva; `null` = raíz), `Factor` (`decimal?`), `MetodoRedondeo`
  (`short?`), `ReglaRedondeo` (`string?`). Campos `ExtMonto`, `RndFrmtInt`, `RndFrmtDec`
  existen en la entidad pero **no** están en ningún DTO ni pantalla ni lógica → se ignoran.
- **Taxonomía de redondeo** (definida hoy en `Web.UI/Controllers/ListadosPrecioController.cs`,
  `CargarDropdownsAsync`):
  - `MetodoRedondeo`: `0`/null = ninguno · `1` = a la unidad (1.00) · `2` = a 0.5 · `3` = a 5 ·
    `4` = a 10 · `5` = a 25. Es el **múltiplo** al que se redondea.
  - `ReglaRedondeo`: `"F"` = hacia abajo · `"C"` = hacia arriba · `"R"`/null = al más cercano.
- **`SocioNegocio.NumLstPrecio`** (`int?`, FK `fk_sn_num_lst_precio` → `ListadoPrecio.Entry`).
  Presente en `SocioNegocioDTO` de API y de `Web.ApiClient` → llega al front como
  `numLstPrecio` (camelCase).
- **`Articulo.PrecioUnitario`** (`decimal?`). Presente en `ArticuloDTO` (API y Web) →
  `precioUnitario`.
- **`ListadoPrecioDomain` / `IListadoPrecioDomain`** existen y están en DI
  (`Startup.cs:132-134`): tienen `ObtenerAsync(int id)` y `ObtenerTodoAsync()` (`IQueryable`).
- DI: `IRepositorioGenerico<Articulo, string>` y `IRepositorioGenerico<ListadoPrecio, int>`
  registrados (`Startup.cs:124,132`), `AddTransient`. Patrón del proyecto: `IXxxDomain` +
  `IXxxApplication` registrados uno tras otro en `Startup.cs`.

### API — artículo

- `ArticuloController` (`api/Articulo`) tiene GET `{codigo}`, `Nombre/{nombre}`,
  `ContengaNombre/{nombre}`, `ContengaCodigo/{codigo}`, `` (todos). Todo va contra
  `IArticuloApplication`, que es una interfaz enfocada (sin lógica de precio).
- `ArticuloDomain.ObtenerPorCodigoAsync(sku)` existe.

### Web — documentos

- Los 7 JS (`cotizaciones, pedidos, entregas, facturas, pedidoscompra, entregascompra,
  facturascompra`) tienen hoy, **idénticos en la zona relevante**:
  - `inicializarBuscadorSocio()` → `App.autocompletar({ ..., endpoint: '/{Doc}/BuscarSocios',
    onSeleccion: s => $('#NombreSn').val(s ? (s.nombre ?? s.Nombre) : '') })`.
  - `function aplicarArticuloSeleccionado(a, origen)` (línea ~360-381) que, entre otras cosas,
    hace `$('#detPrecio').val(a.precioUnitario ?? a.PrecioUnitario ?? 0);` (línea ~372-393).
  - `recalcularLinea()` lee `#detPrecio` y calcula `bruto = cantidad * precio`.
  - Edición de línea existente: `$('#detPrecio').val(linea.precio ?? linea.Precio ?? '')`.
  - Arrays `lineasLocales` (documento nuevo) y `lineasRemotas` (edición). En edición el
    encabezado (socio) es de solo lectura en los 7.
  - La línea solo maneja `#detPrecio` (precio unitario). No hay input de `PrecioBruto` en el
    panel de línea.
- Proxy MVC: cada controller de documento tiene `BuscarSocios`, `BuscarArticulos`,
  `BuscarArticulosPorCodigo` que devuelven `Json(Respuesta<...>)`. `BuscarSocios` devuelve
  `SocioNegocioDTO` (con `numLstPrecio`).
- `site.js` → objeto `App` con `enviarJson`, `autocompletar` (usa `$.get(endpoint, { texto })`),
  `csrfToken`, etc. `App.autocompletar` devuelve `{ establecer(item|null) }` y admite
  `onSeleccion`.
- `Web.ApiClient` tiene `IArticuloApiClient` (verificar métodos reales al implementar).

## §1 — Algoritmo de resolución de precio

**Entrada:** `codigoArticulo` (string), `listaPrecioEntry` (`int?`).
**Salida:** `PrecioResueltoDTO { string Codigo; decimal PrecioBase; decimal FactorEfectivo; decimal PrecioLista; }`.

1. `articulo = ObtenerPorCodigo(codigoArticulo)`. Si no existe → `NotFound`
   (`Respuesta.Resultado = false`, mensaje "Código de articulo no encontrado.").
   `precioBase = articulo.PrecioUnitario ?? 0m`.
2. Si `listaPrecioEntry` es `null` →
   `{ Codigo, PrecioBase = precioBase, FactorEfectivo = 1m, PrecioLista = Round2(precioBase) }`.
3. Con lista:
   1. Recorrer la cadena `Base` empezando por `listaPrecioEntry`:
      - `cadena = []`, `visitados = HashSet<int>()`, `actual = listaPrecioEntry`.
      - Mientras `actual != null`:
        - Si `actual ∈ visitados` → error `400` "La cadena de listas de precio es circular."
        - Si `cadena.Count >= 10` → error `400` "La cadena de listas de precio es demasiado
          profunda."
        - `lp = ListadoPrecio(actual)`; si no existe → error `400` "Lista de precio no
          encontrada." (para la lista raíz pedida; si una `Base` intermedia no existe, mismo
          error).
        - `visitados.Add(actual)`, `cadena.Add(lp)`, `actual = lp.Base`.
   2. `factorEfectivo = cadena.Aggregate(1m, (acc, lp) => acc * (lp.Factor ?? 1m))`.
   3. `sinRedondeo = precioBase * factorEfectivo`.
   4. `precioLista = Redondear(sinRedondeo, cadena[0].MetodoRedondeo, cadena[0].ReglaRedondeo)`
      — usa la config de **la lista asignada** (`cadena[0]`), no la de las bases.
   5. `{ Codigo, PrecioBase = precioBase, FactorEfectivo = factorEfectivo, PrecioLista = precioLista }`.

### `RedondeoCalculadora` (helper puro)

```csharp
public static class RedondeoCalculadora
{
    // metodo: 0/null ninguno, 1 unidad, 2 → 0.5, 3 → 5, 4 → 10, 5 → 25
    // regla:  "F" abajo, "C" arriba, "R"/null al más cercano
    public static decimal Redondear(decimal valor, short? metodo, string? regla)
    {
        decimal paso = (metodo ?? 0) switch
        {
            1 => 1m, 2 => 0.5m, 3 => 5m, 4 => 10m, 5 => 25m, _ => 0m
        };

        decimal resultado;
        if (paso == 0m)
        {
            resultado = valor; // sin múltiplo; solo se cierra a 2 decimales abajo
        }
        else
        {
            var q = valor / paso;
            var qRedondeado = (regla ?? "R").ToUpperInvariant() switch
            {
                "F" => Math.Floor(q),
                "C" => Math.Ceiling(q),
                _   => Math.Round(q, MidpointRounding.AwayFromZero)
            };
            resultado = qRedondeado * paso;
        }
        return Math.Round(resultado, 2, MidpointRounding.AwayFromZero);
    }
}
```

Ubicación sugerida: `API.Domain.Core/Precio/RedondeoCalculadora.cs` (namespace a criterio del
implementer; sin dependencias, `static`).

### `PrecioListaDomain` / `IPrecioListaDomain`

`API.Domain.Core/PrecioListaDomain.cs` + `API.Domain.Interface/IPrecioListaDomain.cs`.

```csharp
public interface IPrecioListaDomain
{
    Task<PrecioResuelto> ResolverAsync(string codigoArticulo, int? listaPrecioEntry);
}
```

`PrecioResuelto` = record/POCO de dominio (`Codigo`, `PrecioBase`, `FactorEfectivo`,
`PrecioLista`). Dependencias por constructor: `IRepositorioGenerico<Articulo, string>` (o
`IArticuloDomain.ObtenerPorCodigoAsync`) y `IListadoPrecioDomain` (tiene `ObtenerAsync(int)`).
Los errores de negocio se lanzan como `Exception` con mensaje (patrón del proyecto — el
Application los captura y arma `Respuesta`). "Artículo no encontrado" se distingue para que el
controller devuelva `404` y no `400` (p. ej. `PrecioResuelto` nulo, o excepción tipada
`ArticuloNoEncontradoException` como en INV-2).

### `PrecioListaApplication` / `IPrecioListaApplication`

`API.Application.Main/PrecioListaApplication.cs` + interfaz en `API.Application.Interface`.

```csharp
Task<Respuesta<PrecioResueltoDTO>> ResolverAsync(string codigoArticulo, int? listaPrecioEntry);
```

Mapea `PrecioResuelto` → `PrecioResueltoDTO` (AutoMapper o manual). `try/catch` como el resto
de los Application: excepción → `Respuesta { Resultado = false, Mensaje = ex.Message }`.

### DTO

`API.Application.DTO/precio/PrecioResueltoDTO.cs`:

```csharp
public class PrecioResueltoDTO
{
    public string Codigo { get; set; } = null!;
    public decimal PrecioBase { get; set; }
    public decimal FactorEfectivo { get; set; }
    public decimal PrecioLista { get; set; }
}
```

### Endpoint

En `ArticuloController` (inyectar `IPrecioListaApplication` — **no** ampliar
`IArticuloApplication`):

```csharp
[HttpGet("{codigo}/Precio")]
public async Task<ActionResult<Respuesta<PrecioResueltoDTO>>> Precio(
    [FromRoute] string codigo, [FromQuery] int? listaPrecio)
{
    var res = await _precioListaApplication.ResolverAsync(codigo, listaPrecio);
    if (!res.Resultado)
        return res.Mensaje?.Contains("no encontrado", StringComparison.OrdinalIgnoreCase) == true
            ? NotFound(res) : BadRequest(res);
    return Ok(res);
}
```

(El criterio de `404` vs `400` puede resolverse mejor con excepción tipada; el contrato es:
artículo inexistente → `404`; lista inexistente / cadena circular / profunda → `400`; OK → `200`
con `PrecioResueltoDTO`.)

### DI (`Startup.cs`)

```csharp
services.AddTransient<IPrecioListaDomain, PrecioListaDomain>();
services.AddTransient<IPrecioListaApplication, PrecioListaApplication>();
```

(`IRepositorioGenerico<Articulo,string>`, `IRepositorioGenerico<ListadoPrecio,int>` e
`IListadoPrecioDomain` ya están.)

## §2 — API: pruebas

### `RedondeoCalculadora` (unit, tabla)

| valor | método | regla | esperado |
|------:|:------:|:-----:|---------:|
| 26.4  | 0 / null | cualquiera | 26.40 |
| 26.4  | 1 | R | 26.00 |
| 26.4  | 1 | C | 27.00 |
| 26.4  | 2 | R | 26.50 |
| 26.4  | 3 | R | 25.00 |
| 26.4  | 3 | C | 30.00 |
| 26.4  | 3 | F | 25.00 |
| 12.5  | 2 | R | 12.50 |
| 24    | 5 | C | 25.00 |

### `PrecioListaDomain.ResolverAsync` (unit, repos/domains mockeados)

- `listaPrecio = null` → `PrecioLista == PrecioUnitario` (redondeado a 2), `FactorEfectivo == 1`.
- Lista con `Factor = 1.20`, sin redondeo, artículo `PrecioUnitario = 22` → `PrecioLista == 26.40`,
  `FactorEfectivo == 1.20`.
- Cadena `L0(Factor 1.10, Base=L1)` → `L1(Factor 1.20, Base=null)`, artículo `100` →
  `FactorEfectivo == 1.32`, `PrecioLista == 132.00`.
- `L0.Base = L0` (ciclo directo) → excepción "circular".
- Cadena de 11 niveles → excepción "demasiado profunda".
- Artículo inexistente → resultado que el Application traduce a `404`.
- `Factor = null` → se trata como `1`.
- `PrecioUnitario = null` → `PrecioBase == 0` → `PrecioLista == 0`.
- Redondeo real: `Factor = 1.20`, `MetodoRedondeo = 3` (a 5), `ReglaRedondeo = "C"`, artículo
  `22` → `sinRedondeo = 26.4` → `PrecioLista == 30.00`.

### `ArticuloController` (test de controller, patrón `ListadoPrecioControllerTests`)

- `GET api/Articulo/0003-0002/Precio?listaPrecio=<venta>` con Application mockeado → `200 Ok` con
  `PrecioLista == 26.40`.
- `GET api/Articulo/NOEXISTE/Precio` → `404`.
- `GET api/Articulo/0003-0002/Precio?listaPrecio=999` (lista inválida) → `400`.
- `GET api/Articulo/0003-0002/Precio` (sin `listaPrecio`) → `200` con `PrecioLista ==` precio
  base redondeado.

Los tests existentes no se tocan (todo es aditivo). Verificar baseline de la suite antes y
después.

## §3 — Web

### `Web.ApiClient`

- **`Dtos/Precio/PrecioResueltoDTO.cs`** — espejo del DTO de la API.
- **`IArticuloApiClient` / `ArticuloApiClient`:**
  `Task<RespuestaDTO<PrecioResueltoDTO>> ObtenerPrecioAsync(string codigo, int? listaPrecio)`
  → `GET api/Articulo/{codigo}/Precio?listaPrecio={listaPrecio}` (omitir el query si es null).
  Seguir el patrón/tipo de `RespuestaDTO<T>` que ya usan los demás métodos del cliente.

### `Web.UI` — proxy

**Un** `PreciosController` nuevo (evita añadir una acción a los 7 controllers de documento):

```csharp
[Authorize]
public class PreciosController : Controller
{
    private readonly IArticuloApiClient _articulos;
    public PreciosController(IArticuloApiClient articulos) => _articulos = articulos;

    [HttpGet]
    public async Task<IActionResult> Articulo(string codigo, int? listaPrecio)
        => Json(await _articulos.ObtenerPrecioAsync(codigo, listaPrecio));
}
```

Ruta resultante: `GET /Precios/Articulo?codigo=...&listaPrecio=...`.

### `site.js` — helper compartido

Agregar a `App`:

```javascript
/**
 * Resuelve el precio de un artículo según una lista de precio.
 * Devuelve el número (precioLista) o null si no hay lista, el artículo no existe,
 * o la API falla -- el llamador cae entonces al PrecioUnitario del artículo.
 */
resolverPrecioLista: async function (codigo, listaPrecio) {
    if (!codigo || listaPrecio == null || listaPrecio === '') return null;
    try {
        const r = await $.get('/Precios/Articulo', { codigo, listaPrecio });
        if (r && r.resultado && r.dato && r.dato.precioLista != null) {
            return Number(r.dato.precioLista);
        }
    } catch (e) { /* cae a null */ }
    return null;
}
```

### Los 7 JS de documento — mismo cambio en cada uno

`{doc}` ∈ `{cotizaciones, pedidos, entregas, facturas, pedidoscompra, entregascompra,
facturascompra}`.

1. **Variable de módulo** `let listaPrecioSocio = null;` (junto a las demás `let` del módulo de
   detalle).

2. **`inicializarBuscadorSocio` → `onSeleccion`:** hoy es
   `s => $('#NombreSn').val(s ? (s.nombre ?? s.Nombre) : '')`. Cambiar a una función que además:

   ```javascript
   onSeleccion: async s => {
       $('#NombreSn').val(s ? (s.nombre ?? s.Nombre) : '');
       const nueva = s ? (s.numLstPrecio ?? s.NumLstPrecio ?? null) : null;
       if (nueva === listaPrecioSocio) return;
       listaPrecioSocio = nueva;
       await ofrecerRecalcularLineas();   // ver punto 4
   }
   ```

3. **`aplicarArticuloSeleccionado(a, origen)`:** la línea
   `$('#detPrecio').val(a.precioUnitario ?? a.PrecioUnitario ?? 0);` pasa a:

   ```javascript
   const codArt = a.codigo ?? a.Codigo;
   const pLista = await App.resolverPrecioLista(codArt, listaPrecioSocio);
   $('#detPrecio').val(pLista != null ? pLista : (a.precioUnitario ?? a.PrecioUnitario ?? 0));
   recalcularLinea();
   ```

   (La función ya es `async` en la práctica o se la marca `async`; `aplicarArticuloSeleccionado`
   se invoca desde `onSeleccion` del buscador, que no espera retorno — aceptable.)

4. **Recalcular líneas al cambiar el socio** — `ofrecerRecalcularLineas()`:

   ```javascript
   async function ofrecerRecalcularLineas() {
       const lista = esEdicionDetalle() ? lineasRemotas : lineasLocales;
       if (!lista.length || listaPrecioSocio == null) return;
       const ok = await Swal.fire({
           icon: 'question',
           title: '¿Actualizar precios?',
           text: 'Se recalcularán los precios de las líneas según la lista de precio del socio seleccionado.',
           showCancelButton: true, confirmButtonText: 'Actualizar', cancelButtonText: 'Mantener'
       }).then(r => r.isConfirmed);
       if (!ok) return;
       for (const l of lista) {
           const cod = l.codArticulo ?? l.CodArticulo;
           const p = await App.resolverPrecioLista(cod, listaPrecioSocio);
           if (p != null) { l.precio = p; l.Precio = p; }
       }
       pintarDetalle();   // el nombre real del re-render de la tabla de líneas en cada .js
   }
   ```

   En **edición** el socio del encabezado es de solo lectura → `onSeleccion` no se dispara →
   `ofrecerRecalcularLineas` nunca corre ahí. En **creación** `lineasLocales` es el array a tocar.
   Verificar en cada `.js` el nombre real de la función de re-render (`pintarDetalle`,
   `renderizarLineas`, etc.) y de `esEdicionDetalle`.

   Si el usuario **borra** el socio (`s == null`): `listaPrecioSocio = null`, no se ofrece
   recálculo (las líneas ya cargadas conservan su precio) y las líneas nuevas vuelven a tomar el
   `PrecioUnitario` base.

5. **Precarga en edición:** al abrir un documento existente, setear `listaPrecioSocio` con la
   lista del socio del documento para que, si el usuario **agrega** una línea nueva, tome el
   precio de lista. El encabezado ya trae `CodigoSn`; resolver su lista con una llamada a
   `/Precios/...` no aplica (es de artículo). Opciones:
   - (a) Traer `NumLstPrecio` en el DTO del documento / en la respuesta del `Obtener` del
     documento y usarlo directo. Requiere revisar si el `...DTO` del documento ya expone algo
     del socio; si no, **(b)**.
   - (b) Endpoint liviano en `PreciosController`:
     `GET /Precios/ListaDeSocio?codigo={codigoSn}` → proxy a
     `api/SocioNegocio/{codigo}` y devolver solo `numLstPrecio`. El `.js` lo llama en el
     `cargar()` de edición.
   - **Recomendado: (b)** — no toca los DTO de los 7 documentos ni la API de documentos.
     Añade `ObtenerPorCodigoAsync` al `ISocioNegocioApiClient` si no está (probablemente sí).

No se toca `PrecioBruto` (no hay input; se deriva). No se toca `recalcularLinea` salvo la llamada
explícita que ya se agrega tras fijar `#detPrecio`.

### Web — pruebas

No hay pruebas automatizadas de JS/MVC en el repo Web. Checklist manual (navegador), documento
canónico + smoke en el resto:

- Cliente **con** lista "Venta" (Factor 1.20, sin redondeo): elegir `0003-0002` → `#detPrecio` =
  `26.40`. Editar a mano a `25` → se respeta.
- Cliente **sin** lista asignada: elegir `0003-0002` → `#detPrecio` = `22.00` (precio base).
- Con 2 líneas cargadas, cambiar a otro cliente con lista distinta → aparece el `Swal` de
  confirmación; "Actualizar" recalcula ambas; "Mantener" las deja.
- API caída / lista borrada: elegir artículo → cae al precio base, no bloquea.
- Repetir el "camino feliz" en un documento de **compra** con un proveedor que tenga lista.

## §4 — Estructura del plan

Un spec (este) + un plan de implementación (`docs/superpowers/plans/2026-09-09-lista-de-precio-en-documentos-implementation.md`),
ejecución subagent-driven (igual que INV-2/3/4 y ajustes-documentos). Bosquejo de tareas:

1. **API — núcleo de precio:** `RedondeoCalculadora` + tests · `PrecioResuelto` /
   `IPrecioListaDomain` / `PrecioListaDomain` + tests · `PrecioResueltoDTO` /
   `IPrecioListaApplication` / `PrecioListaApplication` + tests · endpoint en `ArticuloController`
   + tests · DI en `Startup.cs`. `dotnet build API.sln` + suite (baseline → baseline + N).
2. **Web — plomería compartida:** `PrecioResueltoDTO` (Web) · `IArticuloApiClient.ObtenerPrecioAsync`
   · `PreciosController` (`Articulo` + `ListaDeSocio`) · `App.resolverPrecioLista` en `site.js`.
   `dotnet build Web.slnx`.
3. **`cotizaciones.js` canónico (§3):** variable `listaPrecioSocio`, `onSeleccion` del socio,
   `aplicarArticuloSeleccionado`, `ofrecerRecalcularLineas`, precarga en edición.
4. **`pedidos.js`** = transformación de (3).
5. **`entregas.js`** = transformación de (3).
6. **`facturas.js`** = transformación de (3).
7. **`pedidoscompra.js`** = transformación de (3).
8. **`entregascompra.js`** = transformación de (3).
9. **`facturascompra.js`** = transformación de (3).
10. **Verificación conjunta:** `dotnet build API.sln` + suite completa (baseline + N de §1/§2) +
    `dotnet build Web.slnx` + checklist manual del §3.

Cada tarea de transformación (4-9) verifica la precondición (que el `.js` destino tiene hoy la
forma esperada en `aplicarArticuloSeleccionado` / `inicializarBuscadorSocio` / arrays de líneas)
y aplica la misma tabla de sustitución que (3), adaptando nombres locales (`pintarDetalle`,
`esEdicionDetalle`, etc.).

## Riesgos y fuera de alcance

- Los 7 `.js` son paralelos en la zona de socio/artículo/líneas pero **no idénticos** en el resto
  (creación en dos pasos vs líneas embebidas). La transformación se aplica solo a esas zonas.
- El endpoint hace 1 resolución por artículo elegido y N por recálculo al cambiar socio. Para el
  volumen de la empresa es aceptable; no se añade caché.
- El redondeo usa la config de la **lista asignada**, no de las bases (decisión explícita; SAP B1
  redondea con la lista del interlocutor).
- Fuera de alcance: precios por artículo dentro de una lista (equivalente a `ITM1` de SAP B1);
  descuento del socio (`SocioNegocio.Descuento`); recálculo de documentos **ya guardados**;
  campos `ExtMonto` / `RndFrmtInt` / `RndFrmtDec`; seed de datos de listas (se usa la pantalla
  existente); documentos de mercancía (Entrada/Salida — no llevan socio ni precio de venta).
- `Factor` y `MetodoRedondeo`/`ReglaRedondeo` nulos se tratan como neutros (`1`, sin redondeo de
  múltiplo). `PrecioUnitario` nulo → base `0`.
