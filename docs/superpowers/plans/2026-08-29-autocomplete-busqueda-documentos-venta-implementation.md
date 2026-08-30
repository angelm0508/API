# Buscador con autocompletado en Cotización/Pedido/Entrega/Factura — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reemplazar los `<select>` de Socio de Negocio (encabezado) y Artículo/Almacén/Impuesto (línea de detalle) por un buscador con autocompletado contra la API, en Cotización, Pedido, Entrega y Factura.

**Architecture:** Un widget jQuery reutilizable (`App.autocompletar`, en `site.js`) conecta un `<input type="text">` visible + un `<input type="hidden">` con el código real + un `<ul>` de sugerencias, ya declarados en el markup de cada campo. Busca contra la API vía acciones proxy delgadas ya existentes por convención en cada controller de documento (mismo patrón que hoy usan para cargar sus dropdowns). Impuesto gana en la API el endpoint de búsqueda por nombre que Artículo/SocioNegocio/Almacén ya tienen.

**Tech Stack:** .NET 7 (API), .NET 8 (Web), jQuery + Bootstrap 5 (sin librerías nuevas).

**Spec:** [docs/superpowers/specs/2026-08-29-autocomplete-busqueda-documentos-venta-design.md](../specs/2026-08-29-autocomplete-busqueda-documentos-venta-design.md)

## Global Constraints

- Búsqueda contra la API (no cargar todo el catálogo y filtrar en el navegador), con debounce de 300ms y mínimo 2 caracteres (0 para Impuesto, que muestra todo el catálogo apenas se hace foco en el campo).
- Se preservan tal cual los efectos secundarios ya existentes: autocompletar Nombre del Socio de Negocio; Descripción/Precio/Almacén por defecto del Artículo; tasa de Impuesto para el cálculo de la línea.
- Si el usuario escribe texto sin elegir una sugerencia válida y trata de pasar a otro campo del mismo formulario, se bloquea esa salida (campo en rojo + mensaje) hasta que elija una opción real o vacíe el campo. Cerrar/Cancelar el modal completo **siempre** se permite, sin importar el estado del buscador.
- Formato de sugerencia uniforme: `"Código - Nombre"` para Socio de Negocio/Artículo/Almacén; `"Nombre (tasa%)"` para Impuesto.
- No se agrega ninguna librería de terceros.

---

### Task 1: API — `Impuesto` gana búsqueda por nombre (`ContengaNombre`)

**Files:**
- Modify: `API.Domain.Interface/IImpuestoDomain.cs`
- Modify: `API.Domain.Core/ImpuestoDomain.cs`
- Modify: `API.Application.Interface/IImpuestoApplication.cs`
- Modify: `API.Application.Main/ImpuestoApplication.cs`
- Modify: `API.Service.WebApi/Controllers/ImpuestoController.cs`
- Test: `API.Service.WebApi.Tests/Controllers/ImpuestoControllerTests.cs`

**Interfaces:**
- Produces: `IImpuestoApplication.ObtenerContengaNombreAsync(string nombre)` → `Task<Respuesta<IEnumerable<ImpuestoDTO>>>`, usado por el Task 2 (`ImpuestoApiClient` en el repo Web) a través del endpoint `GET api/Impuesto/ContengaNombre/{nombre}`.

- [ ] **Step 1: Escribir las pruebas que fallan**

Buscar el archivo `API.Service.WebApi.Tests/Controllers/ImpuestoControllerTests.cs` (ya existe, tiene pruebas de `ObtenerPorCodigo`/`ObtenerTodo`/`Crear`/`Actualizar`/`Eliminar`) y agregar, siguiendo el mismo patrón ya usado en `AlmacenControllerTests.cs` para `ObtenerContengaNombre`:

```csharp
[Fact]
public async Task ObtenerContengaNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
{
    var respuesta = new Respuesta<IEnumerable<ImpuestoDTO>> { Resultado = false, Mensaje = "error" };
    _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("IVA")).ReturnsAsync(respuesta);

    var resultado = await _controller.ObtenerContengaNombre("IVA");

    var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
    Assert.Same(respuesta, badRequest.Value);
}

[Fact]
public async Task ObtenerContengaNombre_DevuelveOk_CuandoResultadoEsExitoso()
{
    var respuesta = new Respuesta<IEnumerable<ImpuestoDTO>> { Resultado = true, Dato = new List<ImpuestoDTO> { new ImpuestoDTO { Codigo = "I1", Nombre = "IVA", Tasa = 12 } } };
    _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("IVA")).ReturnsAsync(respuesta);

    var resultado = await _controller.ObtenerContengaNombre("IVA");

    var ok = Assert.IsType<OkObjectResult>(resultado.Result);
    Assert.Same(respuesta, ok.Value);
}
```

- [ ] **Step 2: Ejecutar las pruebas y confirmar que fallan**

```bash
dotnet test API.Service.WebApi.Tests --filter "FullyQualifiedName~ObtenerContengaNombre"
```

Esperado: error de compilación (`IImpuestoApplication` no tiene `ObtenerContengaNombreAsync`, `ImpuestoController` no tiene `ObtenerContengaNombre`).

- [ ] **Step 3: Implementar el cambio**

`API.Domain.Interface/IImpuestoDomain.cs` — agregar antes de `#endregion`:
```csharp
        Task<IEnumerable<Impuesto>> ObtenerContengaNombreAsync(string nombre);
```

`API.Domain.Core/ImpuestoDomain.cs` — agregar antes del `#endregion` final:
```csharp
        public async Task<IEnumerable<Impuesto>> ObtenerContengaNombreAsync(string nombre)
        {
            var impuestos = await _repoImpuesto.ObtenerTodoAsync();
            return await impuestos.Where(x => x.Nombre.Contains(nombre)).ToListAsync();
        }
```
(Agregar `using Microsoft.EntityFrameworkCore;` si no está ya presente en el archivo — sí lo está, ya se usa para `FirstOrDefaultAsync`.)

`API.Application.Interface/IImpuestoApplication.cs` — agregar antes de `#endregion`:
```csharp
        Task<Respuesta<IEnumerable<ImpuestoDTO>>> ObtenerContengaNombreAsync(string nombre);
```

`API.Application.Main/ImpuestoApplication.cs` — agregar antes del `#endregion` final:
```csharp
        public async Task<Respuesta<IEnumerable<ImpuestoDTO>>> ObtenerContengaNombreAsync(string nombre)
        {
            var respuesta = new Respuesta<IEnumerable<ImpuestoDTO>>();
            try
            {
                var impuestos = await _impuestoDomain.ObtenerContengaNombreAsync(nombre);
                respuesta.Dato = _mapper.Map<IEnumerable<ImpuestoDTO>>(impuestos);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }
```

`API.Service.WebApi/Controllers/ImpuestoController.cs` — agregar, entre `ObtenerPorCodigo` y `ObtenerTodo`:
```csharp
        [HttpGet("ContengaNombre/{nombre}")]
        public async Task<ActionResult<Respuesta<IEnumerable<ImpuestoDTO>>>> ObtenerContengaNombre([FromRoute] string nombre)
        {
            var impuestos = await _impuestoApplication.ObtenerContengaNombreAsync(nombre);

            if (!impuestos.Resultado)
                return BadRequest(impuestos);

            return Ok(impuestos);
        }
```

- [ ] **Step 4: Ejecutar las pruebas y confirmar que pasan**

```bash
dotnet test API.Service.WebApi.Tests --filter "FullyQualifiedName~ObtenerContengaNombre"
```

Esperado: 2/2 PASS (más las que ya existían de Almacén, que coinciden por nombre parcial).

- [ ] **Step 5: Build completo y suite completa**

```bash
dotnet build API.sln
dotnet test API.Service.WebApi.Tests
```

Esperado: 0 errores; toda la suite en verde (504 pruebas existentes + 2 nuevas = 506).

- [ ] **Step 6: Commit**

```bash
git add API.Domain.Interface/IImpuestoDomain.cs API.Domain.Core/ImpuestoDomain.cs \
        API.Application.Interface/IImpuestoApplication.cs API.Application.Main/ImpuestoApplication.cs \
        API.Service.WebApi/Controllers/ImpuestoController.cs \
        API.Service.WebApi.Tests/Controllers/ImpuestoControllerTests.cs
git commit -m "feat: Impuesto gana busqueda por nombre (ContengaNombre), mismo patron que Almacen"
```

---

### Task 2: Web.ApiClient — exponer la búsqueda por nombre y el lookup por código de Impuesto

**Files:**
- Modify: `Web.ApiClient/Clientes/IArticuloApiClient.cs`
- Modify: `Web.ApiClient/Clientes/ArticuloApiClient.cs`
- Modify: `Web.ApiClient/Clientes/ISocioNegocioApiClient.cs`
- Modify: `Web.ApiClient/Clientes/SocioNegocioApiClient.cs`
- Modify: `Web.ApiClient/Clientes/IAlmacenApiClient.cs`
- Modify: `Web.ApiClient/Clientes/AlmacenApiClient.cs`
- Modify: `Web.ApiClient/Clientes/IImpuestoApiClient.cs`
- Modify: `Web.ApiClient/Clientes/ImpuestoApiClient.cs`

**Interfaces:**
- Consumes: `GET api/{Recurso}/ContengaNombre/{nombre}` (ya existe para Articulo/SocioNegocio/Almacen; nuevo desde el Task 1 para Impuesto). `GET api/Impuesto/{codigo}` (ya existe en la API, solo faltaba exponerlo en este cliente).
- Produces: `IArticuloApiClient.ObtenerContenganNombreAsync(string)`, `ISocioNegocioApiClient.ObtenerContenganNombreAsync(string)`, `IAlmacenApiClient.ObtenerContenganNombreAsync(string)`, `IImpuestoApiClient.ObtenerContenganNombreAsync(string)` (todas `Task<Respuesta<IEnumerable<TDTO>>>`) e `IImpuestoApiClient.ObtenerAsync(string codigo)` (`Task<Respuesta<ImpuestoDTO>>`) — usados por el Task 3 en adelante desde los controllers de Web.UI.

No hay proyecto de pruebas unitarias en este repo (confirmado en trabajo previo de esta sesión); la verificación es el build.

- [ ] **Step 1: `IArticuloApiClient`/`ArticuloApiClient`**

En `Web.ApiClient/Clientes/IArticuloApiClient.cs`, agregar antes del `}` de cierre de la interfaz:
```csharp
        Task<Respuesta<IEnumerable<ArticuloDTO>>> ObtenerContenganNombreAsync(string nombre);
```

En `Web.ApiClient/Clientes/ArticuloApiClient.cs`, agregar antes del `}` de cierre de la clase:
```csharp
        public Task<Respuesta<IEnumerable<ArticuloDTO>>> ObtenerContenganNombreAsync(string nombre) =>
            GetAsync<IEnumerable<ArticuloDTO>>($"{Recurso}/ContengaNombre/{Uri.EscapeDataString(nombre)}");
```

- [ ] **Step 2: `ISocioNegocioApiClient`/`SocioNegocioApiClient`**

En `Web.ApiClient/Clientes/ISocioNegocioApiClient.cs`, agregar antes del `}` de cierre:
```csharp
        Task<Respuesta<IEnumerable<SocioNegocioDTO>>> ObtenerContenganNombreAsync(string nombre);
```

En `Web.ApiClient/Clientes/SocioNegocioApiClient.cs`, agregar antes del `}` de cierre:
```csharp
        public Task<Respuesta<IEnumerable<SocioNegocioDTO>>> ObtenerContenganNombreAsync(string nombre) =>
            GetAsync<IEnumerable<SocioNegocioDTO>>($"{Recurso}/ContengaNombre/{Uri.EscapeDataString(nombre)}");
```

- [ ] **Step 3: `IAlmacenApiClient`/`AlmacenApiClient`**

En `Web.ApiClient/Clientes/IAlmacenApiClient.cs`, agregar antes del `}` de cierre:
```csharp
        Task<Respuesta<IEnumerable<AlmacenDTO>>> ObtenerContenganNombreAsync(string nombre);
```

En `Web.ApiClient/Clientes/AlmacenApiClient.cs`, agregar antes del `}` de cierre:
```csharp
        public Task<Respuesta<IEnumerable<AlmacenDTO>>> ObtenerContenganNombreAsync(string nombre) =>
            GetAsync<IEnumerable<AlmacenDTO>>($"{Recurso}/ContengaNombre/{Uri.EscapeDataString(nombre)}");
```

- [ ] **Step 4: `IImpuestoApiClient`/`ImpuestoApiClient` — gana `ObtenerAsync(codigo)` y `ObtenerContenganNombreAsync`**

Reemplazar el contenido completo de `Web.ApiClient/Clientes/IImpuestoApiClient.cs`:
```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.Impuesto;

namespace Web.ApiClient.Clientes
{
    public interface IImpuestoApiClient
    {
        Task<Respuesta<IEnumerable<ImpuestoDTO>>> ObtenerTodoAsync();
        Task<Respuesta<ImpuestoDTO>> ObtenerAsync(string codigo);
        Task<Respuesta<IEnumerable<ImpuestoDTO>>> ObtenerContenganNombreAsync(string nombre);
    }
}
```

Reemplazar el contenido completo de `Web.ApiClient/Clientes/ImpuestoApiClient.cs`:
```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.Impuesto;

namespace Web.ApiClient.Clientes
{
    public class ImpuestoApiClient : ApiClientBase, IImpuestoApiClient
    {
        private const string Recurso = "api/Impuesto";

        public ImpuestoApiClient(HttpClient http) : base(http) { }

        public Task<Respuesta<IEnumerable<ImpuestoDTO>>> ObtenerTodoAsync() =>
            GetAsync<IEnumerable<ImpuestoDTO>>(Recurso);

        public Task<Respuesta<ImpuestoDTO>> ObtenerAsync(string codigo) =>
            GetAsync<ImpuestoDTO>($"{Recurso}/{codigo}");

        public Task<Respuesta<IEnumerable<ImpuestoDTO>>> ObtenerContenganNombreAsync(string nombre) =>
            GetAsync<IEnumerable<ImpuestoDTO>>($"{Recurso}/ContengaNombre/{Uri.EscapeDataString(nombre)}");
    }
}
```
(El comentario `// Solo lectura, usado como fuente de dropdown...` se quita porque ya deja de ser cierto -- ahora también se usa para el buscador con autocompletado.)

- [ ] **Step 5: Build**

```bash
dotnet build Web.slnx
```

Esperado: 0 errores.

- [ ] **Step 6: Commit**

```bash
git add Web.ApiClient/Clientes/IArticuloApiClient.cs Web.ApiClient/Clientes/ArticuloApiClient.cs \
        Web.ApiClient/Clientes/ISocioNegocioApiClient.cs Web.ApiClient/Clientes/SocioNegocioApiClient.cs \
        Web.ApiClient/Clientes/IAlmacenApiClient.cs Web.ApiClient/Clientes/AlmacenApiClient.cs \
        Web.ApiClient/Clientes/IImpuestoApiClient.cs Web.ApiClient/Clientes/ImpuestoApiClient.cs
git commit -m "feat: exponer busqueda por nombre (y lookup por codigo de Impuesto) en los clientes de Articulo/SocioNegocio/Almacen/Impuesto"
```

---

### Task 3: Web.UI — Widget `App.autocompletar` + Cotizaciones (slice completo)

Este task construye el widget reutilizable y lo aplica por completo a Cotizaciones (los 4 campos: Socio de Negocio, Artículo, Almacén, Impuesto). Los Tasks 4-6 replican exactamente este mismo patrón en Pedidos/Entregas/Facturas.

**Files:**
- Modify: `Web.UI/wwwroot/js/site.js`
- Modify: `Web.UI/Controllers/CotizacionesController.cs`
- Modify: `Web.UI/Views/Cotizaciones/_Form.cshtml`
- Modify: `Web.UI/wwwroot/js/cotizaciones.js`

**Interfaces:**
- Consumes: `IArticuloApiClient.ObtenerContenganNombreAsync`, `ISocioNegocioApiClient.ObtenerContenganNombreAsync`, `IAlmacenApiClient.ObtenerContenganNombreAsync`/`ObtenerAsync`, `IImpuestoApiClient.ObtenerContenganNombreAsync`/`ObtenerAsync` (Task 2).
- Produces: `App.autocompletar(opciones)` — función global en `site.js`, namespace `App`, usada tal cual por los Tasks 4-6. Firma exacta:
  ```javascript
  // opciones: { texto: jQuery, oculto: jQuery, lista: jQuery, error: jQuery, endpoint: string,
  //             obtenerCodigo: (item) => string, obtenerEtiqueta: (item) => string,
  //             onSeleccion?: (item|null) => void, minCaracteres?: number (def. 2),
  //             debounceMs?: number (def. 300), maxResultados?: number (def. 10) }
  // Devuelve: { establecer: (item|null) => void }
  ```
- Produces (en `CotizacionesController.cs`): acciones `BuscarSocios`, `BuscarArticulos`, `BuscarAlmacenes`, `BuscarImpuestos`, `ObtenerAlmacenPorCodigo`, `ObtenerImpuestoPorCodigo` — mismo esqueleto reutilizado por los Tasks 4-6 en sus propios controllers.

No hay proyecto de pruebas unitarias en este repo; la verificación de este task es build + prueba manual en navegador (Step final).

- [ ] **Step 1: Agregar el widget `App.autocompletar` a `site.js`**

En `Web.UI/wwwroot/js/site.js`, agregar un nuevo método dentro del objeto `App` (después de `eliminar`, separado por coma, antes del `};` de cierre):

```javascript
    eliminar: async function (url) {
        try {
            const respuesta = await fetch(url, {
                method: 'POST',
                headers: { 'X-CSRF-TOKEN': App.csrfToken() }
            });
            return await respuesta.json();
        } catch (e) {
            return { resultado: false, mensaje: 'No se pudo conectar con el servidor.' };
        }
    },

    /**
     * Convierte un <input type="text"> en un buscador con autocompletado contra un endpoint de
     * la API. No depende de ninguna librería externa. Los elementos (texto visible, oculto con
     * el código real, lista de sugerencias, mensaje de error) ya deben existir en el markup --
     * este helper solo los conecta, no crea nada nuevo en el DOM.
     *
     * @param {object} opciones
     * @param {jQuery} opciones.texto - <input type="text"> visible donde se escribe.
     * @param {jQuery} opciones.oculto - <input type="hidden"> donde queda el código real elegido.
     * @param {jQuery} opciones.lista - <ul> donde se pintan las sugerencias.
     * @param {jQuery} opciones.error - elemento con el mensaje de error (se muestra/oculta).
     * @param {string} opciones.endpoint - URL a la que se pide `?texto=...`, responde Respuesta<T[]>.
     * @param {(item: object) => string} opciones.obtenerCodigo
     * @param {(item: object) => string} opciones.obtenerEtiqueta
     * @param {(item: object|null) => void} [opciones.onSeleccion] - recibe el objeto completo
     *        elegido, o null si el campo quedó vacío.
     * @param {number} [opciones.minCaracteres=2]
     * @param {number} [opciones.debounceMs=300]
     * @param {number} [opciones.maxResultados=10]
     * @returns {{ establecer: (item: object|null) => void }} para precargar el campo (ej. al editar).
     */
    autocompletar: function (opciones) {
        const $texto = opciones.texto;
        const $oculto = opciones.oculto;
        const $lista = opciones.lista;
        const $error = opciones.error;
        const endpoint = opciones.endpoint;
        const obtenerCodigo = opciones.obtenerCodigo;
        const obtenerEtiqueta = opciones.obtenerEtiqueta;
        const onSeleccion = opciones.onSeleccion || function () {};
        const minCaracteres = opciones.minCaracteres ?? 2;
        const debounceMs = opciones.debounceMs ?? 300;
        const maxResultados = opciones.maxResultados ?? 10;

        let resultados = [];
        let resuelto = true;
        let indiceActivo = -1;
        let temporizador = null;
        let cerrandoModal = false;

        const $modal = $texto.closest('.modal');
        $modal.on('hide.bs.modal', () => { cerrandoModal = true; });
        $modal.on('hidden.bs.modal', () => { cerrandoModal = false; });

        function marcarResuelto(valor) {
            resuelto = valor;
            $texto.toggleClass('is-invalid', !valor);
            $error.toggleClass('d-none', valor);
        }

        function ocultarLista() {
            $lista.addClass('d-none').empty();
            indiceActivo = -1;
        }

        function pintarLista() {
            if (resultados.length === 0) {
                ocultarLista();
                return;
            }
            $lista.html(resultados.map((item, i) => `
                <li class="list-group-item list-group-item-action${i === indiceActivo ? ' active' : ''}" data-indice="${i}" style="cursor: pointer;">
                    ${obtenerEtiqueta(item)}
                </li>
            `).join('')).removeClass('d-none');
        }

        function elegir(item) {
            $texto.val(obtenerEtiqueta(item));
            $oculto.val(obtenerCodigo(item)).trigger('change');
            marcarResuelto(true);
            ocultarLista();
            onSeleccion(item);
        }

        function limpiar() {
            $texto.val('');
            $oculto.val('').trigger('change');
            marcarResuelto(true);
            ocultarLista();
            onSeleccion(null);
        }

        async function buscar(texto) {
            const respuesta = await $.get(endpoint, { texto });
            resultados = (respuesta.resultado && respuesta.dato) ? respuesta.dato.slice(0, maxResultados) : [];
            indiceActivo = -1;
            pintarLista();
        }

        $texto.on('input', function () {
            const valor = $texto.val();
            marcarResuelto(valor === '');
            $oculto.val('').trigger('change');
            clearTimeout(temporizador);
            if (valor.length < minCaracteres) {
                ocultarLista();
                return;
            }
            temporizador = setTimeout(() => buscar(valor), debounceMs);
        });

        if (minCaracteres === 0) {
            $texto.on('focus', function () {
                if ($texto.val() === '') buscar('');
            });
        }

        $texto.on('keydown', function (e) {
            if ($lista.hasClass('d-none') || resultados.length === 0) return;
            if (e.key === 'ArrowDown') {
                e.preventDefault();
                indiceActivo = Math.min(indiceActivo + 1, resultados.length - 1);
                pintarLista();
            } else if (e.key === 'ArrowUp') {
                e.preventDefault();
                indiceActivo = Math.max(indiceActivo - 1, 0);
                pintarLista();
            } else if (e.key === 'Enter') {
                e.preventDefault();
                if (indiceActivo >= 0) elegir(resultados[indiceActivo]);
            } else if (e.key === 'Escape') {
                ocultarLista();
            }
        });

        // mousedown (no click) para elegir: evita que el blur del input se dispare antes de poder
        // leer en qué sugerencia se hizo clic (el orden normal de eventos es mousedown -> blur -> click).
        $lista.on('mousedown', 'li', function (e) {
            e.preventDefault();
            const indice = Number($(this).data('indice'));
            elegir(resultados[indice]);
        });

        $texto.on('blur', function () {
            if ($texto.val() === '') {
                limpiar();
                return;
            }
            if (!resuelto && !cerrandoModal) {
                setTimeout(() => $texto.trigger('focus'), 0);
            }
        });

        return {
            establecer: function (item) {
                if (item) {
                    elegir(item);
                } else {
                    limpiar();
                }
            }
        };
    }
};
```

(Nota: el `};` final de arriba reemplaza al `};` que hoy cierra el objeto `App` -- solo se agrega la propiedad `autocompletar` antes de ese cierre, no se duplica.)

- [ ] **Step 2: `CotizacionesController.cs` — acciones proxy nuevas**

Agregar, después de la acción `Eliminar` y antes de `ObtenerDetalle` (o en cualquier punto del archivo, el orden no importa):

```csharp
        [HttpGet]
        public async Task<IActionResult> BuscarSocios(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _socios.ObtenerTodoAsync()
                : await _socios.ObtenerContenganNombreAsync(texto);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> BuscarArticulos(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _articulos.ObtenerTodoAsync()
                : await _articulos.ObtenerContenganNombreAsync(texto);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> BuscarAlmacenes(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _almacenes.ObtenerTodoAsync()
                : await _almacenes.ObtenerContenganNombreAsync(texto);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> BuscarImpuestos(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _impuestos.ObtenerTodoAsync()
                : await _impuestos.ObtenerContenganNombreAsync(texto);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerAlmacenPorCodigo(string codigo)
        {
            var respuesta = await _almacenes.ObtenerAsync(codigo);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerImpuestoPorCodigo(string codigo)
        {
            var respuesta = await _impuestos.ObtenerAsync(codigo);
            return Json(respuesta);
        }
```

Y simplificar `CargarDropdownsAsync` (ya no se necesitan las listas completas de Socios/Artículos/Almacenes/Impuestos para poblar un `<select>` -- el buscador los consulta bajo demanda; Moneda sigue siendo un `<select>` normal, sin cambios):

```csharp
        private async Task CargarDropdownsAsync()
        {
            // Socio de Negocio, Artículo, Almacén e Impuesto ya no se cargan aquí como lista
            // completa -- el buscador con autocompletado los consulta bajo demanda
            // (BuscarSocios/BuscarArticulos/BuscarAlmacenes/BuscarImpuestos). Moneda sigue siendo
            // un <select> normal.
            var monedas = await _monedas.ObtenerTodoAsync();
            ViewBag.Monedas = new SelectList(monedas.Dato ?? [], "Codigo", "Nombre");
        }
```

- [ ] **Step 3: `Cotizaciones/_Form.cshtml` — reemplazar los 4 campos**

Reemplazar el bloque del campo Socio de Negocio:
```html
            <div class="col-md-4">
                <label asp-for="CodigoSn" class="form-label">Socio de negocio</label>
                <select asp-for="CodigoSn" id="selectCodigoSn" class="form-select" asp-items="ViewBag.Socios">
                    <option value="">-- Seleccione --</option>
                </select>
            </div>
```
por:
```html
            <div class="col-md-4 position-relative">
                <label asp-for="CodigoSn" class="form-label">Socio de negocio</label>
                <input type="text" id="CodigoSnTexto" class="form-control" placeholder="Buscar por código o nombre..." autocomplete="off"
                       value="@(esEdicion && !string.IsNullOrEmpty(Model.CodigoSn) ? $"{Model.CodigoSn} - {Model.NombreSn}" : "")" />
                <input type="hidden" asp-for="CodigoSn" />
                <div class="invalid-feedback" id="CodigoSnError">Selecciona una opción de la lista o borra el texto.</div>
                <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="CodigoSnResultados"></ul>
            </div>
```

Reemplazar el bloque de los 3 campos de la línea de detalle:
```html
                <div class="col-md-4">
                    <label class="form-label">Artículo</label>
                    <select id="detCodArticulo" class="form-select">
                        <option value="">-- Seleccione --</option>
                    </select>
                </div>
                <div class="col-md-4">
                    <label class="form-label">Almacén</label>
                    <select name="CodAlmacen" id="detCodAlmacen" class="form-select" asp-items="ViewBag.Almacenes">
                        <option value="">-- Seleccione --</option>
                    </select>
                </div>
                <div class="col-md-4">
                    <label class="form-label">Impuesto</label>
                    <select id="detCodigoImpuesto" class="form-select">
                        <option value="">-- Ninguno --</option>
                    </select>
                </div>
```
por:
```html
                <div class="col-md-4 position-relative">
                    <label class="form-label">Artículo</label>
                    <input type="text" id="detCodArticuloTexto" class="form-control" placeholder="Buscar por código o nombre..." autocomplete="off" />
                    <input type="hidden" id="detCodArticulo" />
                    <div class="invalid-feedback" id="detCodArticuloError">Selecciona una opción de la lista o borra el texto.</div>
                    <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="detCodArticuloResultados"></ul>
                </div>
                <div class="col-md-4 position-relative">
                    <label class="form-label">Almacén</label>
                    <input type="text" id="detCodAlmacenTexto" class="form-control" placeholder="Buscar por código o nombre..." autocomplete="off" />
                    <input type="hidden" name="CodAlmacen" id="detCodAlmacen" />
                    <div class="invalid-feedback" id="detCodAlmacenError">Selecciona una opción de la lista o borra el texto.</div>
                    <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="detCodAlmacenResultados"></ul>
                </div>
                <div class="col-md-4 position-relative">
                    <label class="form-label">Impuesto</label>
                    <input type="text" id="detCodigoImpuestoTexto" class="form-control" placeholder="Buscar por nombre..." autocomplete="off" />
                    <input type="hidden" id="detCodigoImpuesto" />
                    <div class="invalid-feedback" id="detCodigoImpuestoError">Selecciona una opción de la lista o borra el texto.</div>
                    <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="detCodigoImpuestoResultados"></ul>
                </div>
```

Eliminar por completo los dos `<script>` de datos (ya no se usan, el buscador consulta bajo demanda):
```html
    <script id="datosArticulosCotizacion" type="application/json">
        @Html.Raw(JsonSerializer.Serialize(ViewBag.Articulos, opcionesJson))
    </script>
    <script id="datosImpuestosCotizacion" type="application/json">
        @Html.Raw(JsonSerializer.Serialize(ViewBag.Impuestos, opcionesJson))
    </script>
```
(El `<script id="datosSeriesCotizacion" ...>` que queda justo debajo, dentro del mismo `@if (!esEdicion) { ... }`, **no se toca** -- sigue haciendo falta para la serie.)

- [ ] **Step 4: `cotizaciones.js` — declaraciones y `abrirModal`**

Cambiar:
```javascript
    let lineasLocales = [];
    let lineasRemotas = [];
    let proximoIdLocal = 1;
    let noLineaOriginalEnEdicion = null;
    let articulosDisponibles = [];
    let impuestosDisponibles = [];
```
por:
```javascript
    let lineasLocales = [];
    let lineasRemotas = [];
    let proximoIdLocal = 1;
    let noLineaOriginalEnEdicion = null;
    let tasaImpuestoSeleccionado = 0;
    let buscadorArticulo, buscadorAlmacen, buscadorImpuesto;
```

Cambiar:
```javascript
    function abrirModal(html) {
        $('#contenidoModal').html(html);
        new bootstrap.Modal('#modalFormulario').show();
        inicializarSerieCotizacion();
        inicializarDetalle();
    }
```
por:
```javascript
    function abrirModal(html) {
        $('#contenidoModal').html(html);
        new bootstrap.Modal('#modalFormulario').show();
        inicializarSerieCotizacion();
        inicializarBuscadorSocio();
        inicializarDetalle();
    }

    function inicializarBuscadorSocio() {
        if ($('#CodigoSnTexto').length === 0) return;
        App.autocompletar({
            texto: $('#CodigoSnTexto'),
            oculto: $('#CodigoSn'),
            lista: $('#CodigoSnResultados'),
            error: $('#CodigoSnError'),
            endpoint: '/Cotizaciones/BuscarSocios',
            obtenerCodigo: s => s.codigo ?? s.Codigo,
            obtenerEtiqueta: s => `${s.codigo ?? s.Codigo} - ${s.nombre ?? s.Nombre}`,
            onSeleccion: s => $('#NombreSn').val(s ? (s.nombre ?? s.Nombre) : '')
        });
    }
```

- [ ] **Step 5: `cotizaciones.js` — `inicializarDetalle`**

Reemplazar el cuerpo completo de `inicializarDetalle` (desde la declaración de `datosArt`/`datosImp` hasta el `if (esEdicionDetalle())` final, sin tocar la primera línea `lineasLocales = [];` ni las 3 siguientes, ni el `if`/`else` final):

```javascript
    function inicializarDetalle() {
        lineasLocales = [];
        lineasRemotas = [];
        proximoIdLocal = 1;
        noLineaOriginalEnEdicion = null;

        const $tabla = $('#tblDetalleCotizacion');
        if ($tabla.length === 0) return;

        buscadorArticulo = App.autocompletar({
            texto: $('#detCodArticuloTexto'), oculto: $('#detCodArticulo'),
            lista: $('#detCodArticuloResultados'), error: $('#detCodArticuloError'),
            endpoint: '/Cotizaciones/BuscarArticulos',
            obtenerCodigo: a => a.codigo ?? a.Codigo,
            obtenerEtiqueta: a => `${a.codigo ?? a.Codigo} - ${a.nombre ?? a.Nombre}`,
            onSeleccion: a => {
                if (!a) return;
                $('#detDescripcion').val(a.nombre ?? a.Nombre ?? '');
                $('#detPrecio').val(a.precioUnitario ?? a.PrecioUnitario ?? 0);
                const almacenDefecto = a.almacenDefecto ?? a.AlmacenDefecto ?? '';
                $('#detCodAlmacenTexto').val(almacenDefecto);
                $('#detCodAlmacen').val(almacenDefecto);
                recalcularLinea();
            }
        });

        buscadorAlmacen = App.autocompletar({
            texto: $('#detCodAlmacenTexto'), oculto: $('#detCodAlmacen'),
            lista: $('#detCodAlmacenResultados'), error: $('#detCodAlmacenError'),
            endpoint: '/Cotizaciones/BuscarAlmacenes',
            obtenerCodigo: al => al.codigo ?? al.Codigo,
            obtenerEtiqueta: al => `${al.codigo ?? al.Codigo} - ${al.nombre ?? al.Nombre}`
        });

        buscadorImpuesto = App.autocompletar({
            texto: $('#detCodigoImpuestoTexto'), oculto: $('#detCodigoImpuesto'),
            lista: $('#detCodigoImpuestoResultados'), error: $('#detCodigoImpuestoError'),
            endpoint: '/Cotizaciones/BuscarImpuestos',
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
```

- [ ] **Step 6: `cotizaciones.js` — `recalcularLinea`, quitar el handler viejo de `detCodArticulo`, y ajustar el binding de recálculo**

Cambiar:
```javascript
    function recalcularLinea() {
        const cantidad = Number($('#detCantidad').val()) || 0;
        const precio = Number($('#detPrecio').val()) || 0;
        const prctjeDesc = Number($('#detPrctjeDesc').val()) || 0;
        const tasa = Number($('#detCodigoImpuesto').find('option:selected').data('tasa')) || 0;
```
por:
```javascript
    function recalcularLinea() {
        const cantidad = Number($('#detCantidad').val()) || 0;
        const precio = Number($('#detPrecio').val()) || 0;
        const prctjeDesc = Number($('#detPrctjeDesc').val()) || 0;
        const tasa = tasaImpuestoSeleccionado || 0;
```

Cambiar:
```javascript
    $(document).on('input change', '#detCantidad, #detPrecio, #detPrctjeDesc, #detCodigoImpuesto', recalcularLinea);

    $(document).on('change', '#detCodArticulo', function () {
        const codigo = $(this).val();
        const articulo = articulosDisponibles.find(a => (a.codigo ?? a.Codigo) === codigo);
        if (articulo) {
            $('#detDescripcion').val(articulo.nombre ?? articulo.Nombre ?? '');
            $('#detPrecio').val(articulo.precioUnitario ?? articulo.PrecioUnitario ?? 0);
            $('#detCodAlmacen').val(articulo.almacenDefecto ?? articulo.AlmacenDefecto ?? '');
        }
        recalcularLinea();
    });
```
por:
```javascript
    $(document).on('input change', '#detCantidad, #detPrecio, #detPrctjeDesc', recalcularLinea);
```
(El recálculo por Artículo/Impuesto ahora lo dispara directamente el `onSeleccion` de cada buscador, configurado en el Step 5 -- ya no hace falta un handler `change` aparte, ni la búsqueda en `articulosDisponibles`, que se elimina junto con esta función.)

- [ ] **Step 7: `cotizaciones.js` — `limpiarPanelLinea` y `cargarLineaParaEditar`**

Cambiar:
```javascript
    function limpiarPanelLinea() {
        $('#detNoLineaOriginal').val('');
        $('#detCodArticulo').val('');
        $('#detCodAlmacen').val('');
        $('#detCodigoImpuesto').val('');
        $('#detDescripcion').val('');
        $('#detCantidad').val('1');
        $('#detPrecio').val('');
        $('#detPrctjeDesc').val('0');
        $('#detImpuestoMonto').val('');
        $('#detTotalLinea').val('');
        noLineaOriginalEnEdicion = null;
    }
```
por:
```javascript
    function limpiarPanelLinea() {
        $('#detNoLineaOriginal').val('');
        buscadorArticulo.establecer(null);
        buscadorAlmacen.establecer(null);
        buscadorImpuesto.establecer(null);
        $('#detDescripcion').val('');
        $('#detCantidad').val('1');
        $('#detPrecio').val('');
        $('#detPrctjeDesc').val('0');
        $('#detImpuestoMonto').val('');
        $('#detTotalLinea').val('');
        noLineaOriginalEnEdicion = null;
    }
```

Cambiar el handler `.btn-editar-linea` (agregar `async` y reemplazar las 3 líneas que llenaban los `<select>` viejos):
```javascript
    $(document).on('click', '.btn-editar-linea', function () {
        const clave = $(this).data('clave');
        const lista = esEdicionDetalle() ? lineasRemotas : lineasLocales;
        const linea = esEdicionDetalle()
            ? lista.find(l => (l.noLinea ?? l.NoLinea) === clave)
            : lista.find(l => l._id === clave);
        if (!linea) return;

        limpiarPanelLinea();
        noLineaOriginalEnEdicion = clave;

        $('#detNoLineaOriginal').val(clave);
        $('#detCodArticulo').val(linea.codArticulo ?? linea.CodArticulo ?? '');
        $('#detCodAlmacen').val(linea.codAlmacen ?? linea.CodAlmacen ?? '');
        $('#detCodigoImpuesto').val(linea.codigoImpuesto ?? linea.CodigoImpuesto ?? '');
        $('#detDescripcion').val(linea.descripcion ?? linea.Descripcion ?? '');
        $('#detCantidad').val(linea.cantidad ?? linea.Cantidad ?? 1);
        $('#detPrecio').val(linea.precio ?? linea.Precio ?? '');
        $('#detPrctjeDesc').val(linea.prctjeDesc ?? linea.PrctjeDesc ?? 0);

        recalcularLinea();
        $('#panelLineaDetalle').removeClass('d-none');
    });
```
por:
```javascript
    $(document).on('click', '.btn-editar-linea', async function () {
        const clave = $(this).data('clave');
        const lista = esEdicionDetalle() ? lineasRemotas : lineasLocales;
        const linea = esEdicionDetalle()
            ? lista.find(l => (l.noLinea ?? l.NoLinea) === clave)
            : lista.find(l => l._id === clave);
        if (!linea) return;

        limpiarPanelLinea();
        noLineaOriginalEnEdicion = clave;

        const codArticulo = linea.codArticulo ?? linea.CodArticulo ?? '';
        const codAlmacen = linea.codAlmacen ?? linea.CodAlmacen ?? '';
        const codigoImpuesto = linea.codigoImpuesto ?? linea.CodigoImpuesto ?? '';

        $('#detNoLineaOriginal').val(clave);

        // El artículo ya trae su propia descripción guardada en la línea -- no hace falta
        // consultar la API para mostrar "Código - Nombre" en el buscador.
        buscadorArticulo.establecer(codArticulo ? { codigo: codArticulo, nombre: linea.descripcion ?? linea.Descripcion ?? '' } : null);

        // Almacén e Impuesto solo guardan el código en la línea -- se consulta una vez por código
        // para poder mostrar el nombre en el buscador (mismo patrón que ya usa el formulario de
        // edición para mostrar el nombre de la serie actual).
        if (codAlmacen) {
            const respuestaAlmacen = await $.get('/Cotizaciones/ObtenerAlmacenPorCodigo', { codigo: codAlmacen });
            buscadorAlmacen.establecer(respuestaAlmacen.resultado && respuestaAlmacen.dato ? respuestaAlmacen.dato : { codigo: codAlmacen, nombre: codAlmacen });
        } else {
            buscadorAlmacen.establecer(null);
        }

        if (codigoImpuesto) {
            const respuestaImpuesto = await $.get('/Cotizaciones/ObtenerImpuestoPorCodigo', { codigo: codigoImpuesto });
            buscadorImpuesto.establecer(respuestaImpuesto.resultado && respuestaImpuesto.dato ? respuestaImpuesto.dato : { codigo: codigoImpuesto, nombre: codigoImpuesto, tasa: 0 });
        } else {
            buscadorImpuesto.establecer(null);
        }

        $('#detDescripcion').val(linea.descripcion ?? linea.Descripcion ?? '');
        $('#detCantidad').val(linea.cantidad ?? linea.Cantidad ?? 1);
        $('#detPrecio').val(linea.precio ?? linea.Precio ?? '');
        $('#detPrctjeDesc').val(linea.prctjeDesc ?? linea.PrctjeDesc ?? 0);

        recalcularLinea();
        $('#panelLineaDetalle').removeClass('d-none');
    });
```
(`datosForm.CodArticulo = $('#detCodArticulo').val() || null;` y `datosForm.CodigoImpuesto = $('#detCodigoImpuesto').val() || null;`, dentro de `btnGuardarLinea`, **no cambian** -- siguen leyendo del mismo id, que ahora es el `<input type="hidden">`.)

- [ ] **Step 8: Build**

```bash
dotnet build Web.slnx
```

Esperado: 0 errores.

- [ ] **Step 9: Verificación manual en navegador**

Con la API y Web corriendo (build aislado en puertos que no choquen con Visual Studio, admin/Admin123!):
1. Abrir "Nueva cotización", buscar y elegir un Socio de Negocio -- confirmar que autocompleta Nombre.
2. Agregar una línea: buscar y elegir un Artículo -- confirmar que autocompleta Descripción, Precio y Almacén (texto + oculto).
3. Cambiar el Almacén manualmente buscando otro.
4. Hacer foco en el campo Impuesto sin escribir nada -- confirmar que aparece el catálogo completo; elegir uno y confirmar que el monto de impuesto/total de la línea se recalcula.
5. Escribir texto en el buscador de Artículo sin elegir ninguna sugerencia y presionar Tab -- confirmar que el campo se marca en rojo y el foco no se mueve; borrar el texto y confirmar que sí se puede salir; cerrar el modal con el buscador en ese mismo estado inválido y confirmar que el modal sí cierra.
6. Guardar la cotización, reabrirla para editar, editar la línea agregada -- confirmar que los 4 buscadores muestran "Código - Nombre" correctamente.

- [ ] **Step 10: Commit**

```bash
git add Web.UI/wwwroot/js/site.js Web.UI/Controllers/CotizacionesController.cs \
        Web.UI/Views/Cotizaciones/_Form.cshtml Web.UI/wwwroot/js/cotizaciones.js
git commit -m "feat: buscador con autocompletado para Socio de Negocio/Articulo/Almacen/Impuesto en Cotizaciones"
```

---

### Task 4: Web.UI — Pedidos (mismo patrón que Cotizaciones)

Réplica exacta del Task 3 (Steps 2-7) para Pedidos. El widget `App.autocompletar` ya existe (Task 3, Step 1) y no se toca aquí.

**Files:**
- Modify: `Web.UI/Controllers/PedidosController.cs`
- Modify: `Web.UI/Views/Pedidos/_Form.cshtml`
- Modify: `Web.UI/wwwroot/js/pedidos.js`

**Interfaces:**
- Consumes: `App.autocompletar` (Task 3).

- [ ] **Step 1: `PedidosController.cs` — acciones proxy y `CargarDropdownsAsync`**

Agregar (mismo esqueleto que Task 3 Step 2, sustituyendo `_socios`/`_articulos`/`_almacenes`/`_impuestos`, ya inyectados en este controller):

```csharp
        [HttpGet]
        public async Task<IActionResult> BuscarSocios(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _socios.ObtenerTodoAsync()
                : await _socios.ObtenerContenganNombreAsync(texto);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> BuscarArticulos(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _articulos.ObtenerTodoAsync()
                : await _articulos.ObtenerContenganNombreAsync(texto);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> BuscarAlmacenes(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _almacenes.ObtenerTodoAsync()
                : await _almacenes.ObtenerContenganNombreAsync(texto);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> BuscarImpuestos(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _impuestos.ObtenerTodoAsync()
                : await _impuestos.ObtenerContenganNombreAsync(texto);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerAlmacenPorCodigo(string codigo)
        {
            var respuesta = await _almacenes.ObtenerAsync(codigo);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerImpuestoPorCodigo(string codigo)
        {
            var respuesta = await _impuestos.ObtenerAsync(codigo);
            return Json(respuesta);
        }
```

Y simplificar `CargarDropdownsAsync`:
```csharp
        private async Task CargarDropdownsAsync()
        {
            // Socio de Negocio, Artículo, Almacén e Impuesto ya no se cargan aquí como lista
            // completa -- el buscador con autocompletado los consulta bajo demanda
            // (BuscarSocios/BuscarArticulos/BuscarAlmacenes/BuscarImpuestos). Moneda sigue siendo
            // un <select> normal.
            var monedas = await _monedas.ObtenerTodoAsync();
            ViewBag.Monedas = new SelectList(monedas.Dato ?? [], "Codigo", "Nombre");
        }
```

- [ ] **Step 2: `Pedidos/_Form.cshtml` — reemplazar los 4 campos**

Reemplazar el campo Socio de Negocio:
```html
            <div class="col-md-4">
                <label asp-for="CodigoSn" class="form-label">Socio de negocio</label>
                <select asp-for="CodigoSn" id="selectCodigoSn" class="form-select" asp-items="ViewBag.Socios">
                    <option value="">-- Seleccione --</option>
                </select>
            </div>
```
por:
```html
            <div class="col-md-4 position-relative">
                <label asp-for="CodigoSn" class="form-label">Socio de negocio</label>
                <input type="text" id="CodigoSnTexto" class="form-control" placeholder="Buscar por código o nombre..." autocomplete="off"
                       value="@(esEdicion && !string.IsNullOrEmpty(Model.CodigoSn) ? $"{Model.CodigoSn} - {Model.NombreSn}" : "")" />
                <input type="hidden" asp-for="CodigoSn" />
                <div class="invalid-feedback" id="CodigoSnError">Selecciona una opción de la lista o borra el texto.</div>
                <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="CodigoSnResultados"></ul>
            </div>
```

Reemplazar los 3 campos de la línea de detalle:
```html
                <div class="col-md-4">
                    <label class="form-label">Artículo</label>
                    <select id="detCodArticulo" class="form-select">
                        <option value="">-- Seleccione --</option>
                    </select>
                </div>
                <div class="col-md-4">
                    <label class="form-label">Almacén</label>
                    <select name="CodAlmacen" id="detCodAlmacen" class="form-select" asp-items="ViewBag.Almacenes">
                        <option value="">-- Seleccione --</option>
                    </select>
                </div>
                <div class="col-md-4">
                    <label class="form-label">Impuesto</label>
                    <select id="detCodigoImpuesto" class="form-select">
                        <option value="">-- Ninguno --</option>
                    </select>
                </div>
```
por:
```html
                <div class="col-md-4 position-relative">
                    <label class="form-label">Artículo</label>
                    <input type="text" id="detCodArticuloTexto" class="form-control" placeholder="Buscar por código o nombre..." autocomplete="off" />
                    <input type="hidden" id="detCodArticulo" />
                    <div class="invalid-feedback" id="detCodArticuloError">Selecciona una opción de la lista o borra el texto.</div>
                    <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="detCodArticuloResultados"></ul>
                </div>
                <div class="col-md-4 position-relative">
                    <label class="form-label">Almacén</label>
                    <input type="text" id="detCodAlmacenTexto" class="form-control" placeholder="Buscar por código o nombre..." autocomplete="off" />
                    <input type="hidden" name="CodAlmacen" id="detCodAlmacen" />
                    <div class="invalid-feedback" id="detCodAlmacenError">Selecciona una opción de la lista o borra el texto.</div>
                    <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="detCodAlmacenResultados"></ul>
                </div>
                <div class="col-md-4 position-relative">
                    <label class="form-label">Impuesto</label>
                    <input type="text" id="detCodigoImpuestoTexto" class="form-control" placeholder="Buscar por nombre..." autocomplete="off" />
                    <input type="hidden" id="detCodigoImpuesto" />
                    <div class="invalid-feedback" id="detCodigoImpuestoError">Selecciona una opción de la lista o borra el texto.</div>
                    <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="detCodigoImpuestoResultados"></ul>
                </div>
```

Eliminar los 2 `<script>` de datos (`datosArticulosPedido`, `datosImpuestosPedido`); dejar intacto `datosSeriesPedido`.

- [ ] **Step 3: `pedidos.js` — declaraciones y `abrirModal`**

Cambiar:
```javascript
    let lineasLocales = [];
    let lineasRemotas = [];
    let proximoIdLocal = 1;
    let noLineaOriginalEnEdicion = null;
    let articulosDisponibles = [];
    let impuestosDisponibles = [];
```
por:
```javascript
    let lineasLocales = [];
    let lineasRemotas = [];
    let proximoIdLocal = 1;
    let noLineaOriginalEnEdicion = null;
    let tasaImpuestoSeleccionado = 0;
    let buscadorArticulo, buscadorAlmacen, buscadorImpuesto;
```

Cambiar:
```javascript
    function abrirModal(html) {
        $('#contenidoModal').html(html);
        new bootstrap.Modal('#modalFormulario').show();
        inicializarSeriePedido();
        inicializarDetalle();
    }
```
por:
```javascript
    function abrirModal(html) {
        $('#contenidoModal').html(html);
        new bootstrap.Modal('#modalFormulario').show();
        inicializarSeriePedido();
        inicializarBuscadorSocio();
        inicializarDetalle();
    }

    function inicializarBuscadorSocio() {
        if ($('#CodigoSnTexto').length === 0) return;
        App.autocompletar({
            texto: $('#CodigoSnTexto'),
            oculto: $('#CodigoSn'),
            lista: $('#CodigoSnResultados'),
            error: $('#CodigoSnError'),
            endpoint: '/Pedidos/BuscarSocios',
            obtenerCodigo: s => s.codigo ?? s.Codigo,
            obtenerEtiqueta: s => `${s.codigo ?? s.Codigo} - ${s.nombre ?? s.Nombre}`,
            onSeleccion: s => $('#NombreSn').val(s ? (s.nombre ?? s.Nombre) : '')
        });
    }
```

- [ ] **Step 4: `pedidos.js` — `inicializarDetalle`**

Mismo reemplazo que el Task 3 Step 5, cambiando únicamente los 4 endpoints de `/Cotizaciones/Buscar*` a `/Pedidos/Buscar*`:

```javascript
    function inicializarDetalle() {
        lineasLocales = [];
        lineasRemotas = [];
        proximoIdLocal = 1;
        noLineaOriginalEnEdicion = null;

        const $tabla = $('#tblDetallePedido');
        if ($tabla.length === 0) return;

        buscadorArticulo = App.autocompletar({
            texto: $('#detCodArticuloTexto'), oculto: $('#detCodArticulo'),
            lista: $('#detCodArticuloResultados'), error: $('#detCodArticuloError'),
            endpoint: '/Pedidos/BuscarArticulos',
            obtenerCodigo: a => a.codigo ?? a.Codigo,
            obtenerEtiqueta: a => `${a.codigo ?? a.Codigo} - ${a.nombre ?? a.Nombre}`,
            onSeleccion: a => {
                if (!a) return;
                $('#detDescripcion').val(a.nombre ?? a.Nombre ?? '');
                $('#detPrecio').val(a.precioUnitario ?? a.PrecioUnitario ?? 0);
                const almacenDefecto = a.almacenDefecto ?? a.AlmacenDefecto ?? '';
                $('#detCodAlmacenTexto').val(almacenDefecto);
                $('#detCodAlmacen').val(almacenDefecto);
                recalcularLinea();
            }
        });

        buscadorAlmacen = App.autocompletar({
            texto: $('#detCodAlmacenTexto'), oculto: $('#detCodAlmacen'),
            lista: $('#detCodAlmacenResultados'), error: $('#detCodAlmacenError'),
            endpoint: '/Pedidos/BuscarAlmacenes',
            obtenerCodigo: al => al.codigo ?? al.Codigo,
            obtenerEtiqueta: al => `${al.codigo ?? al.Codigo} - ${al.nombre ?? al.Nombre}`
        });

        buscadorImpuesto = App.autocompletar({
            texto: $('#detCodigoImpuestoTexto'), oculto: $('#detCodigoImpuesto'),
            lista: $('#detCodigoImpuestoResultados'), error: $('#detCodigoImpuestoError'),
            endpoint: '/Pedidos/BuscarImpuestos',
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
```

- [ ] **Step 5: `pedidos.js` — `recalcularLinea` y quitar el handler viejo**

Igual que el Task 3 Step 6: cambiar la línea de `tasa` en `recalcularLinea` a `const tasa = tasaImpuestoSeleccionado || 0;`, y reemplazar:
```javascript
    $(document).on('input change', '#detCantidad, #detPrecio, #detPrctjeDesc, #detCodigoImpuesto', recalcularLinea);

    $(document).on('change', '#detCodArticulo', function () {
        const codigo = $(this).val();
        const articulo = articulosDisponibles.find(a => (a.codigo ?? a.Codigo) === codigo);
        if (articulo) {
            $('#detDescripcion').val(articulo.nombre ?? articulo.Nombre ?? '');
            $('#detPrecio').val(articulo.precioUnitario ?? articulo.PrecioUnitario ?? 0);
            $('#detCodAlmacen').val(articulo.almacenDefecto ?? articulo.AlmacenDefecto ?? '');
        }
        recalcularLinea();
    });
```
por:
```javascript
    $(document).on('input change', '#detCantidad, #detPrecio, #detPrctjeDesc', recalcularLinea);
```

- [ ] **Step 6: `pedidos.js` — `limpiarPanelLinea` y `cargarLineaParaEditar`**

Mismo reemplazo que el Task 3 Step 7 (idéntico, sin ningún endpoint incrustado que cambie -- los dos `$.get` usan rutas relativas al controller actual):

```javascript
    function limpiarPanelLinea() {
        $('#detNoLineaOriginal').val('');
        buscadorArticulo.establecer(null);
        buscadorAlmacen.establecer(null);
        buscadorImpuesto.establecer(null);
        $('#detDescripcion').val('');
        $('#detCantidad').val('1');
        $('#detPrecio').val('');
        $('#detPrctjeDesc').val('0');
        $('#detImpuestoMonto').val('');
        $('#detTotalLinea').val('');
        noLineaOriginalEnEdicion = null;
    }
```

```javascript
    $(document).on('click', '.btn-editar-linea', async function () {
        const clave = $(this).data('clave');
        const lista = esEdicionDetalle() ? lineasRemotas : lineasLocales;
        const linea = esEdicionDetalle()
            ? lista.find(l => (l.noLinea ?? l.NoLinea) === clave)
            : lista.find(l => l._id === clave);
        if (!linea) return;

        limpiarPanelLinea();
        noLineaOriginalEnEdicion = clave;

        const codArticulo = linea.codArticulo ?? linea.CodArticulo ?? '';
        const codAlmacen = linea.codAlmacen ?? linea.CodAlmacen ?? '';
        const codigoImpuesto = linea.codigoImpuesto ?? linea.CodigoImpuesto ?? '';

        $('#detNoLineaOriginal').val(clave);

        buscadorArticulo.establecer(codArticulo ? { codigo: codArticulo, nombre: linea.descripcion ?? linea.Descripcion ?? '' } : null);

        if (codAlmacen) {
            const respuestaAlmacen = await $.get('/Pedidos/ObtenerAlmacenPorCodigo', { codigo: codAlmacen });
            buscadorAlmacen.establecer(respuestaAlmacen.resultado && respuestaAlmacen.dato ? respuestaAlmacen.dato : { codigo: codAlmacen, nombre: codAlmacen });
        } else {
            buscadorAlmacen.establecer(null);
        }

        if (codigoImpuesto) {
            const respuestaImpuesto = await $.get('/Pedidos/ObtenerImpuestoPorCodigo', { codigo: codigoImpuesto });
            buscadorImpuesto.establecer(respuestaImpuesto.resultado && respuestaImpuesto.dato ? respuestaImpuesto.dato : { codigo: codigoImpuesto, nombre: codigoImpuesto, tasa: 0 });
        } else {
            buscadorImpuesto.establecer(null);
        }

        $('#detDescripcion').val(linea.descripcion ?? linea.Descripcion ?? '');
        $('#detCantidad').val(linea.cantidad ?? linea.Cantidad ?? 1);
        $('#detPrecio').val(linea.precio ?? linea.Precio ?? '');
        $('#detPrctjeDesc').val(linea.prctjeDesc ?? linea.PrctjeDesc ?? 0);

        recalcularLinea();
        $('#panelLineaDetalle').removeClass('d-none');
    });
```

- [ ] **Step 7: Build**

```bash
dotnet build Web.slnx
```

Esperado: 0 errores.

- [ ] **Step 8: Verificación manual en navegador**

Mismos 6 puntos del Task 3 Step 9, sobre la pantalla de Pedidos.

- [ ] **Step 9: Commit**

```bash
git add Web.UI/Controllers/PedidosController.cs Web.UI/Views/Pedidos/_Form.cshtml Web.UI/wwwroot/js/pedidos.js
git commit -m "feat: buscador con autocompletado para Socio de Negocio/Articulo/Almacen/Impuesto en Pedidos"
```

---

### Task 5: Web.UI — Entregas (mismo patrón que Cotizaciones/Pedidos)

Idéntico al Task 4, sustituyendo `Pedido`→`Entrega`, `pedido`→`entrega`, `/Pedidos/`→`/Entregas/`, `PedidosController`→`EntregasController`, `pedidos.js`→`entregas.js`, `Pedidos/_Form.cshtml`→`Entregas/_Form.cshtml`, `tblDetallePedido`→`tblDetalleEntrega`.

**Files:**
- Modify: `Web.UI/Controllers/EntregasController.cs`
- Modify: `Web.UI/Views/Entregas/_Form.cshtml`
- Modify: `Web.UI/wwwroot/js/entregas.js`

**Interfaces:**
- Consumes: `App.autocompletar` (Task 3).

- [ ] **Step 1: `EntregasController.cs` — acciones proxy y `CargarDropdownsAsync`**

```csharp
        [HttpGet]
        public async Task<IActionResult> BuscarSocios(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _socios.ObtenerTodoAsync()
                : await _socios.ObtenerContenganNombreAsync(texto);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> BuscarArticulos(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _articulos.ObtenerTodoAsync()
                : await _articulos.ObtenerContenganNombreAsync(texto);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> BuscarAlmacenes(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _almacenes.ObtenerTodoAsync()
                : await _almacenes.ObtenerContenganNombreAsync(texto);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> BuscarImpuestos(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _impuestos.ObtenerTodoAsync()
                : await _impuestos.ObtenerContenganNombreAsync(texto);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerAlmacenPorCodigo(string codigo)
        {
            var respuesta = await _almacenes.ObtenerAsync(codigo);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerImpuestoPorCodigo(string codigo)
        {
            var respuesta = await _impuestos.ObtenerAsync(codigo);
            return Json(respuesta);
        }
```

```csharp
        private async Task CargarDropdownsAsync()
        {
            // Socio de Negocio, Artículo, Almacén e Impuesto ya no se cargan aquí como lista
            // completa -- el buscador con autocompletado los consulta bajo demanda
            // (BuscarSocios/BuscarArticulos/BuscarAlmacenes/BuscarImpuestos). Moneda sigue siendo
            // un <select> normal.
            var monedas = await _monedas.ObtenerTodoAsync();
            ViewBag.Monedas = new SelectList(monedas.Dato ?? [], "Codigo", "Nombre");
        }
```

- [ ] **Step 2: `Entregas/_Form.cshtml` — reemplazar los 4 campos**

Reemplazar el campo Socio de Negocio:
```html
            <div class="col-md-4">
                <label asp-for="CodigoSn" class="form-label">Socio de negocio</label>
                <select asp-for="CodigoSn" id="selectCodigoSn" class="form-select" asp-items="ViewBag.Socios">
                    <option value="">-- Seleccione --</option>
                </select>
            </div>
```
por:
```html
            <div class="col-md-4 position-relative">
                <label asp-for="CodigoSn" class="form-label">Socio de negocio</label>
                <input type="text" id="CodigoSnTexto" class="form-control" placeholder="Buscar por código o nombre..." autocomplete="off"
                       value="@(esEdicion && !string.IsNullOrEmpty(Model.CodigoSn) ? $"{Model.CodigoSn} - {Model.NombreSn}" : "")" />
                <input type="hidden" asp-for="CodigoSn" />
                <div class="invalid-feedback" id="CodigoSnError">Selecciona una opción de la lista o borra el texto.</div>
                <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="CodigoSnResultados"></ul>
            </div>
```

Reemplazar los 3 campos de la línea de detalle:
```html
                <div class="col-md-4">
                    <label class="form-label">Artículo</label>
                    <select id="detCodArticulo" class="form-select">
                        <option value="">-- Seleccione --</option>
                    </select>
                </div>
                <div class="col-md-4">
                    <label class="form-label">Almacén</label>
                    <select name="CodAlmacen" id="detCodAlmacen" class="form-select" asp-items="ViewBag.Almacenes">
                        <option value="">-- Seleccione --</option>
                    </select>
                </div>
                <div class="col-md-4">
                    <label class="form-label">Impuesto</label>
                    <select id="detCodigoImpuesto" class="form-select">
                        <option value="">-- Ninguno --</option>
                    </select>
                </div>
```
por:
```html
                <div class="col-md-4 position-relative">
                    <label class="form-label">Artículo</label>
                    <input type="text" id="detCodArticuloTexto" class="form-control" placeholder="Buscar por código o nombre..." autocomplete="off" />
                    <input type="hidden" id="detCodArticulo" />
                    <div class="invalid-feedback" id="detCodArticuloError">Selecciona una opción de la lista o borra el texto.</div>
                    <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="detCodArticuloResultados"></ul>
                </div>
                <div class="col-md-4 position-relative">
                    <label class="form-label">Almacén</label>
                    <input type="text" id="detCodAlmacenTexto" class="form-control" placeholder="Buscar por código o nombre..." autocomplete="off" />
                    <input type="hidden" name="CodAlmacen" id="detCodAlmacen" />
                    <div class="invalid-feedback" id="detCodAlmacenError">Selecciona una opción de la lista o borra el texto.</div>
                    <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="detCodAlmacenResultados"></ul>
                </div>
                <div class="col-md-4 position-relative">
                    <label class="form-label">Impuesto</label>
                    <input type="text" id="detCodigoImpuestoTexto" class="form-control" placeholder="Buscar por nombre..." autocomplete="off" />
                    <input type="hidden" id="detCodigoImpuesto" />
                    <div class="invalid-feedback" id="detCodigoImpuestoError">Selecciona una opción de la lista o borra el texto.</div>
                    <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="detCodigoImpuestoResultados"></ul>
                </div>
```

Eliminar los 2 `<script>` de datos (`datosArticulosEntrega`, `datosImpuestosEntrega`); dejar intacto `datosSeriesEntrega`.

- [ ] **Step 3: `entregas.js` — declaraciones y `abrirModal`**

```javascript
    let lineasLocales = [];
    let lineasRemotas = [];
    let proximoIdLocal = 1;
    let noLineaOriginalEnEdicion = null;
    let tasaImpuestoSeleccionado = 0;
    let buscadorArticulo, buscadorAlmacen, buscadorImpuesto;
```

```javascript
    function abrirModal(html) {
        $('#contenidoModal').html(html);
        new bootstrap.Modal('#modalFormulario').show();
        inicializarSerieEntrega();
        inicializarBuscadorSocio();
        inicializarDetalle();
    }

    function inicializarBuscadorSocio() {
        if ($('#CodigoSnTexto').length === 0) return;
        App.autocompletar({
            texto: $('#CodigoSnTexto'),
            oculto: $('#CodigoSn'),
            lista: $('#CodigoSnResultados'),
            error: $('#CodigoSnError'),
            endpoint: '/Entregas/BuscarSocios',
            obtenerCodigo: s => s.codigo ?? s.Codigo,
            obtenerEtiqueta: s => `${s.codigo ?? s.Codigo} - ${s.nombre ?? s.Nombre}`,
            onSeleccion: s => $('#NombreSn').val(s ? (s.nombre ?? s.Nombre) : '')
        });
    }
```

- [ ] **Step 4: `entregas.js` — `inicializarDetalle`**

```javascript
    function inicializarDetalle() {
        lineasLocales = [];
        lineasRemotas = [];
        proximoIdLocal = 1;
        noLineaOriginalEnEdicion = null;

        const $tabla = $('#tblDetalleEntrega');
        if ($tabla.length === 0) return;

        buscadorArticulo = App.autocompletar({
            texto: $('#detCodArticuloTexto'), oculto: $('#detCodArticulo'),
            lista: $('#detCodArticuloResultados'), error: $('#detCodArticuloError'),
            endpoint: '/Entregas/BuscarArticulos',
            obtenerCodigo: a => a.codigo ?? a.Codigo,
            obtenerEtiqueta: a => `${a.codigo ?? a.Codigo} - ${a.nombre ?? a.Nombre}`,
            onSeleccion: a => {
                if (!a) return;
                $('#detDescripcion').val(a.nombre ?? a.Nombre ?? '');
                $('#detPrecio').val(a.precioUnitario ?? a.PrecioUnitario ?? 0);
                const almacenDefecto = a.almacenDefecto ?? a.AlmacenDefecto ?? '';
                $('#detCodAlmacenTexto').val(almacenDefecto);
                $('#detCodAlmacen').val(almacenDefecto);
                recalcularLinea();
            }
        });

        buscadorAlmacen = App.autocompletar({
            texto: $('#detCodAlmacenTexto'), oculto: $('#detCodAlmacen'),
            lista: $('#detCodAlmacenResultados'), error: $('#detCodAlmacenError'),
            endpoint: '/Entregas/BuscarAlmacenes',
            obtenerCodigo: al => al.codigo ?? al.Codigo,
            obtenerEtiqueta: al => `${al.codigo ?? al.Codigo} - ${al.nombre ?? al.Nombre}`
        });

        buscadorImpuesto = App.autocompletar({
            texto: $('#detCodigoImpuestoTexto'), oculto: $('#detCodigoImpuesto'),
            lista: $('#detCodigoImpuestoResultados'), error: $('#detCodigoImpuestoError'),
            endpoint: '/Entregas/BuscarImpuestos',
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
```

- [ ] **Step 5: `entregas.js` — `recalcularLinea` y quitar el handler viejo**

Cambiar la línea de `tasa` en `recalcularLinea` a `const tasa = tasaImpuestoSeleccionado || 0;`, y reemplazar:
```javascript
    $(document).on('input change', '#detCantidad, #detPrecio, #detPrctjeDesc, #detCodigoImpuesto', recalcularLinea);

    $(document).on('change', '#detCodArticulo', function () {
        const codigo = $(this).val();
        const articulo = articulosDisponibles.find(a => (a.codigo ?? a.Codigo) === codigo);
        if (articulo) {
            $('#detDescripcion').val(articulo.nombre ?? articulo.Nombre ?? '');
            $('#detPrecio').val(articulo.precioUnitario ?? articulo.PrecioUnitario ?? 0);
            $('#detCodAlmacen').val(articulo.almacenDefecto ?? articulo.AlmacenDefecto ?? '');
        }
        recalcularLinea();
    });
```
por:
```javascript
    $(document).on('input change', '#detCantidad, #detPrecio, #detPrctjeDesc', recalcularLinea);
```

- [ ] **Step 6: `entregas.js` — `limpiarPanelLinea` y `cargarLineaParaEditar`**

```javascript
    function limpiarPanelLinea() {
        $('#detNoLineaOriginal').val('');
        buscadorArticulo.establecer(null);
        buscadorAlmacen.establecer(null);
        buscadorImpuesto.establecer(null);
        $('#detDescripcion').val('');
        $('#detCantidad').val('1');
        $('#detPrecio').val('');
        $('#detPrctjeDesc').val('0');
        $('#detImpuestoMonto').val('');
        $('#detTotalLinea').val('');
        noLineaOriginalEnEdicion = null;
    }
```

```javascript
    $(document).on('click', '.btn-editar-linea', async function () {
        const clave = $(this).data('clave');
        const lista = esEdicionDetalle() ? lineasRemotas : lineasLocales;
        const linea = esEdicionDetalle()
            ? lista.find(l => (l.noLinea ?? l.NoLinea) === clave)
            : lista.find(l => l._id === clave);
        if (!linea) return;

        limpiarPanelLinea();
        noLineaOriginalEnEdicion = clave;

        const codArticulo = linea.codArticulo ?? linea.CodArticulo ?? '';
        const codAlmacen = linea.codAlmacen ?? linea.CodAlmacen ?? '';
        const codigoImpuesto = linea.codigoImpuesto ?? linea.CodigoImpuesto ?? '';

        $('#detNoLineaOriginal').val(clave);

        buscadorArticulo.establecer(codArticulo ? { codigo: codArticulo, nombre: linea.descripcion ?? linea.Descripcion ?? '' } : null);

        if (codAlmacen) {
            const respuestaAlmacen = await $.get('/Entregas/ObtenerAlmacenPorCodigo', { codigo: codAlmacen });
            buscadorAlmacen.establecer(respuestaAlmacen.resultado && respuestaAlmacen.dato ? respuestaAlmacen.dato : { codigo: codAlmacen, nombre: codAlmacen });
        } else {
            buscadorAlmacen.establecer(null);
        }

        if (codigoImpuesto) {
            const respuestaImpuesto = await $.get('/Entregas/ObtenerImpuestoPorCodigo', { codigo: codigoImpuesto });
            buscadorImpuesto.establecer(respuestaImpuesto.resultado && respuestaImpuesto.dato ? respuestaImpuesto.dato : { codigo: codigoImpuesto, nombre: codigoImpuesto, tasa: 0 });
        } else {
            buscadorImpuesto.establecer(null);
        }

        $('#detDescripcion').val(linea.descripcion ?? linea.Descripcion ?? '');
        $('#detCantidad').val(linea.cantidad ?? linea.Cantidad ?? 1);
        $('#detPrecio').val(linea.precio ?? linea.Precio ?? '');
        $('#detPrctjeDesc').val(linea.prctjeDesc ?? linea.PrctjeDesc ?? 0);

        recalcularLinea();
        $('#panelLineaDetalle').removeClass('d-none');
    });
```

- [ ] **Step 7: Build**

```bash
dotnet build Web.slnx
```

Esperado: 0 errores.

- [ ] **Step 8: Verificación manual en navegador**

Mismos 6 puntos del Task 3 Step 9, sobre la pantalla de Entregas.

- [ ] **Step 9: Commit**

```bash
git add Web.UI/Controllers/EntregasController.cs Web.UI/Views/Entregas/_Form.cshtml Web.UI/wwwroot/js/entregas.js
git commit -m "feat: buscador con autocompletado para Socio de Negocio/Articulo/Almacen/Impuesto en Entregas"
```

---

### Task 6: Web.UI — Facturas (mismo patrón que Cotizaciones/Pedidos/Entregas)

Idéntico al Task 4/5, sustituyendo `Factura`/`factura`/`/Facturas/`/`FacturasController`/`facturas.js`/`Facturas/_Form.cshtml`/`tblDetalleFactura`.

**Files:**
- Modify: `Web.UI/Controllers/FacturasController.cs`
- Modify: `Web.UI/Views/Facturas/_Form.cshtml`
- Modify: `Web.UI/wwwroot/js/facturas.js`

**Interfaces:**
- Consumes: `App.autocompletar` (Task 3).

- [ ] **Step 1: `FacturasController.cs` — acciones proxy y `CargarDropdownsAsync`**

```csharp
        [HttpGet]
        public async Task<IActionResult> BuscarSocios(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _socios.ObtenerTodoAsync()
                : await _socios.ObtenerContenganNombreAsync(texto);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> BuscarArticulos(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _articulos.ObtenerTodoAsync()
                : await _articulos.ObtenerContenganNombreAsync(texto);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> BuscarAlmacenes(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _almacenes.ObtenerTodoAsync()
                : await _almacenes.ObtenerContenganNombreAsync(texto);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> BuscarImpuestos(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _impuestos.ObtenerTodoAsync()
                : await _impuestos.ObtenerContenganNombreAsync(texto);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerAlmacenPorCodigo(string codigo)
        {
            var respuesta = await _almacenes.ObtenerAsync(codigo);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerImpuestoPorCodigo(string codigo)
        {
            var respuesta = await _impuestos.ObtenerAsync(codigo);
            return Json(respuesta);
        }
```

```csharp
        private async Task CargarDropdownsAsync()
        {
            // Socio de Negocio, Artículo, Almacén e Impuesto ya no se cargan aquí como lista
            // completa -- el buscador con autocompletado los consulta bajo demanda
            // (BuscarSocios/BuscarArticulos/BuscarAlmacenes/BuscarImpuestos). Moneda sigue siendo
            // un <select> normal.
            var monedas = await _monedas.ObtenerTodoAsync();
            ViewBag.Monedas = new SelectList(monedas.Dato ?? [], "Codigo", "Nombre");
        }
```

- [ ] **Step 2: `Facturas/_Form.cshtml` — reemplazar los 4 campos**

Reemplazar el campo Socio de Negocio:
```html
            <div class="col-md-4">
                <label asp-for="CodigoSn" class="form-label">Socio de negocio</label>
                <select asp-for="CodigoSn" id="selectCodigoSn" class="form-select" asp-items="ViewBag.Socios">
                    <option value="">-- Seleccione --</option>
                </select>
            </div>
```
por:
```html
            <div class="col-md-4 position-relative">
                <label asp-for="CodigoSn" class="form-label">Socio de negocio</label>
                <input type="text" id="CodigoSnTexto" class="form-control" placeholder="Buscar por código o nombre..." autocomplete="off"
                       value="@(esEdicion && !string.IsNullOrEmpty(Model.CodigoSn) ? $"{Model.CodigoSn} - {Model.NombreSn}" : "")" />
                <input type="hidden" asp-for="CodigoSn" />
                <div class="invalid-feedback" id="CodigoSnError">Selecciona una opción de la lista o borra el texto.</div>
                <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="CodigoSnResultados"></ul>
            </div>
```

Reemplazar los 3 campos de la línea de detalle:
```html
                <div class="col-md-4">
                    <label class="form-label">Artículo</label>
                    <select id="detCodArticulo" class="form-select">
                        <option value="">-- Seleccione --</option>
                    </select>
                </div>
                <div class="col-md-4">
                    <label class="form-label">Almacén</label>
                    <select name="CodAlmacen" id="detCodAlmacen" class="form-select" asp-items="ViewBag.Almacenes">
                        <option value="">-- Seleccione --</option>
                    </select>
                </div>
                <div class="col-md-4">
                    <label class="form-label">Impuesto</label>
                    <select id="detCodigoImpuesto" class="form-select">
                        <option value="">-- Ninguno --</option>
                    </select>
                </div>
```
por:
```html
                <div class="col-md-4 position-relative">
                    <label class="form-label">Artículo</label>
                    <input type="text" id="detCodArticuloTexto" class="form-control" placeholder="Buscar por código o nombre..." autocomplete="off" />
                    <input type="hidden" id="detCodArticulo" />
                    <div class="invalid-feedback" id="detCodArticuloError">Selecciona una opción de la lista o borra el texto.</div>
                    <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="detCodArticuloResultados"></ul>
                </div>
                <div class="col-md-4 position-relative">
                    <label class="form-label">Almacén</label>
                    <input type="text" id="detCodAlmacenTexto" class="form-control" placeholder="Buscar por código o nombre..." autocomplete="off" />
                    <input type="hidden" name="CodAlmacen" id="detCodAlmacen" />
                    <div class="invalid-feedback" id="detCodAlmacenError">Selecciona una opción de la lista o borra el texto.</div>
                    <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="detCodAlmacenResultados"></ul>
                </div>
                <div class="col-md-4 position-relative">
                    <label class="form-label">Impuesto</label>
                    <input type="text" id="detCodigoImpuestoTexto" class="form-control" placeholder="Buscar por nombre..." autocomplete="off" />
                    <input type="hidden" id="detCodigoImpuesto" />
                    <div class="invalid-feedback" id="detCodigoImpuestoError">Selecciona una opción de la lista o borra el texto.</div>
                    <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="detCodigoImpuestoResultados"></ul>
                </div>
```

Eliminar los 2 `<script>` de datos (`datosArticulosFactura`, `datosImpuestosFactura`); dejar intacto `datosSeriesFactura`.

- [ ] **Step 3: `facturas.js` — declaraciones y `abrirModal`**

```javascript
    let lineasLocales = [];
    let lineasRemotas = [];
    let proximoIdLocal = 1;
    let noLineaOriginalEnEdicion = null;
    let tasaImpuestoSeleccionado = 0;
    let buscadorArticulo, buscadorAlmacen, buscadorImpuesto;
```

```javascript
    function abrirModal(html) {
        $('#contenidoModal').html(html);
        new bootstrap.Modal('#modalFormulario').show();
        inicializarSerieFactura();
        inicializarBuscadorSocio();
        inicializarDetalle();
    }

    function inicializarBuscadorSocio() {
        if ($('#CodigoSnTexto').length === 0) return;
        App.autocompletar({
            texto: $('#CodigoSnTexto'),
            oculto: $('#CodigoSn'),
            lista: $('#CodigoSnResultados'),
            error: $('#CodigoSnError'),
            endpoint: '/Facturas/BuscarSocios',
            obtenerCodigo: s => s.codigo ?? s.Codigo,
            obtenerEtiqueta: s => `${s.codigo ?? s.Codigo} - ${s.nombre ?? s.Nombre}`,
            onSeleccion: s => $('#NombreSn').val(s ? (s.nombre ?? s.Nombre) : '')
        });
    }
```

- [ ] **Step 4: `facturas.js` — `inicializarDetalle`**

```javascript
    function inicializarDetalle() {
        lineasLocales = [];
        lineasRemotas = [];
        proximoIdLocal = 1;
        noLineaOriginalEnEdicion = null;

        const $tabla = $('#tblDetalleFactura');
        if ($tabla.length === 0) return;

        buscadorArticulo = App.autocompletar({
            texto: $('#detCodArticuloTexto'), oculto: $('#detCodArticulo'),
            lista: $('#detCodArticuloResultados'), error: $('#detCodArticuloError'),
            endpoint: '/Facturas/BuscarArticulos',
            obtenerCodigo: a => a.codigo ?? a.Codigo,
            obtenerEtiqueta: a => `${a.codigo ?? a.Codigo} - ${a.nombre ?? a.Nombre}`,
            onSeleccion: a => {
                if (!a) return;
                $('#detDescripcion').val(a.nombre ?? a.Nombre ?? '');
                $('#detPrecio').val(a.precioUnitario ?? a.PrecioUnitario ?? 0);
                const almacenDefecto = a.almacenDefecto ?? a.AlmacenDefecto ?? '';
                $('#detCodAlmacenTexto').val(almacenDefecto);
                $('#detCodAlmacen').val(almacenDefecto);
                recalcularLinea();
            }
        });

        buscadorAlmacen = App.autocompletar({
            texto: $('#detCodAlmacenTexto'), oculto: $('#detCodAlmacen'),
            lista: $('#detCodAlmacenResultados'), error: $('#detCodAlmacenError'),
            endpoint: '/Facturas/BuscarAlmacenes',
            obtenerCodigo: al => al.codigo ?? al.Codigo,
            obtenerEtiqueta: al => `${al.codigo ?? al.Codigo} - ${al.nombre ?? al.Nombre}`
        });

        buscadorImpuesto = App.autocompletar({
            texto: $('#detCodigoImpuestoTexto'), oculto: $('#detCodigoImpuesto'),
            lista: $('#detCodigoImpuestoResultados'), error: $('#detCodigoImpuestoError'),
            endpoint: '/Facturas/BuscarImpuestos',
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
```

- [ ] **Step 5: `facturas.js` — `recalcularLinea` y quitar el handler viejo**

Cambiar la línea de `tasa` en `recalcularLinea` a `const tasa = tasaImpuestoSeleccionado || 0;`, y reemplazar:
```javascript
    $(document).on('input change', '#detCantidad, #detPrecio, #detPrctjeDesc, #detCodigoImpuesto', recalcularLinea);

    $(document).on('change', '#detCodArticulo', function () {
        const codigo = $(this).val();
        const articulo = articulosDisponibles.find(a => (a.codigo ?? a.Codigo) === codigo);
        if (articulo) {
            $('#detDescripcion').val(articulo.nombre ?? articulo.Nombre ?? '');
            $('#detPrecio').val(articulo.precioUnitario ?? articulo.PrecioUnitario ?? 0);
            $('#detCodAlmacen').val(articulo.almacenDefecto ?? articulo.AlmacenDefecto ?? '');
        }
        recalcularLinea();
    });
```
por:
```javascript
    $(document).on('input change', '#detCantidad, #detPrecio, #detPrctjeDesc', recalcularLinea);
```

- [ ] **Step 6: `facturas.js` — `limpiarPanelLinea` y `cargarLineaParaEditar`**

```javascript
    function limpiarPanelLinea() {
        $('#detNoLineaOriginal').val('');
        buscadorArticulo.establecer(null);
        buscadorAlmacen.establecer(null);
        buscadorImpuesto.establecer(null);
        $('#detDescripcion').val('');
        $('#detCantidad').val('1');
        $('#detPrecio').val('');
        $('#detPrctjeDesc').val('0');
        $('#detImpuestoMonto').val('');
        $('#detTotalLinea').val('');
        noLineaOriginalEnEdicion = null;
    }
```

```javascript
    $(document).on('click', '.btn-editar-linea', async function () {
        const clave = $(this).data('clave');
        const lista = esEdicionDetalle() ? lineasRemotas : lineasLocales;
        const linea = esEdicionDetalle()
            ? lista.find(l => (l.noLinea ?? l.NoLinea) === clave)
            : lista.find(l => l._id === clave);
        if (!linea) return;

        limpiarPanelLinea();
        noLineaOriginalEnEdicion = clave;

        const codArticulo = linea.codArticulo ?? linea.CodArticulo ?? '';
        const codAlmacen = linea.codAlmacen ?? linea.CodAlmacen ?? '';
        const codigoImpuesto = linea.codigoImpuesto ?? linea.CodigoImpuesto ?? '';

        $('#detNoLineaOriginal').val(clave);

        buscadorArticulo.establecer(codArticulo ? { codigo: codArticulo, nombre: linea.descripcion ?? linea.Descripcion ?? '' } : null);

        if (codAlmacen) {
            const respuestaAlmacen = await $.get('/Facturas/ObtenerAlmacenPorCodigo', { codigo: codAlmacen });
            buscadorAlmacen.establecer(respuestaAlmacen.resultado && respuestaAlmacen.dato ? respuestaAlmacen.dato : { codigo: codAlmacen, nombre: codAlmacen });
        } else {
            buscadorAlmacen.establecer(null);
        }

        if (codigoImpuesto) {
            const respuestaImpuesto = await $.get('/Facturas/ObtenerImpuestoPorCodigo', { codigo: codigoImpuesto });
            buscadorImpuesto.establecer(respuestaImpuesto.resultado && respuestaImpuesto.dato ? respuestaImpuesto.dato : { codigo: codigoImpuesto, nombre: codigoImpuesto, tasa: 0 });
        } else {
            buscadorImpuesto.establecer(null);
        }

        $('#detDescripcion').val(linea.descripcion ?? linea.Descripcion ?? '');
        $('#detCantidad').val(linea.cantidad ?? linea.Cantidad ?? 1);
        $('#detPrecio').val(linea.precio ?? linea.Precio ?? '');
        $('#detPrctjeDesc').val(linea.prctjeDesc ?? linea.PrctjeDesc ?? 0);

        recalcularLinea();
        $('#panelLineaDetalle').removeClass('d-none');
    });
```

- [ ] **Step 7: Build**

```bash
dotnet build Web.slnx
```

Esperado: 0 errores.

- [ ] **Step 8: Verificación manual en navegador**

Mismos 6 puntos del Task 3 Step 9, sobre la pantalla de Facturas.

- [ ] **Step 9: Commit**

```bash
git add Web.UI/Controllers/FacturasController.cs Web.UI/Views/Facturas/_Form.cshtml Web.UI/wwwroot/js/facturas.js
git commit -m "feat: buscador con autocompletado para Socio de Negocio/Articulo/Almacen/Impuesto en Facturas"
```

---

### Task 7: Verificación final

**Files:** ninguno (solo verificación).

- [ ] **Step 1: Suite completa de API**

```bash
dotnet test API.Service.WebApi.Tests
```

Esperado: 100% PASS (506 pruebas: 504 existentes + 2 nuevas del Task 1).

- [ ] **Step 2: Build completo de ambos repos**

```bash
dotnet build API.sln
dotnet build Web.slnx
```

Esperado: 0 errores en ambos.

- [ ] **Step 3: Barrido rápido de consistencia entre los 4 módulos**

```bash
grep -c "App.autocompletar" Web.UI/wwwroot/js/cotizaciones.js Web.UI/wwwroot/js/pedidos.js Web.UI/wwwroot/js/entregas.js Web.UI/wwwroot/js/facturas.js
grep -rn "articulosDisponibles\|impuestosDisponibles\|selectCodigoSn\|detCodArticulo\" class=\"form-select\|data-tasa" Web.UI/wwwroot/js/cotizaciones.js Web.UI/wwwroot/js/pedidos.js Web.UI/wwwroot/js/entregas.js Web.UI/wwwroot/js/facturas.js Web.UI/Views/Cotizaciones/_Form.cshtml Web.UI/Views/Pedidos/_Form.cshtml Web.UI/Views/Entregas/_Form.cshtml Web.UI/Views/Facturas/_Form.cshtml
```

Esperado: el primer comando da `4` en cada archivo (4 buscadores por módulo); el segundo no debe encontrar nada (confirma que no quedó ningún rastro de los `<select>`/arreglos viejos en ninguno de los 4 módulos).

- [ ] **Step 4: Verificación manual final, extremo a extremo, en un solo módulo (Cotizaciones)**

Con la API y Web corriendo (build aislado, admin/Admin123!): crear una cotización completa usando los 4 buscadores (Socio de Negocio, y una línea con Artículo + Almacén + Impuesto elegidos por búsqueda), guardarla, reabrirla para editar, y confirmar que todo se muestra y recalcula correctamente. Repetir el intento de "salir del campo sin elegir" una vez más para reconfirmar el bloqueo tras todos los cambios acumulados.

- [ ] **Step 5: Recordatorio final**

Avisar al usuario que reinicie las sesiones de depuración de Visual Studio (API y Web.UI) para recoger los cambios de esta sesión.
