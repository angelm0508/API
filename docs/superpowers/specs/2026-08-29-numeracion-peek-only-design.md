# Spec: `GenerarCodigoAsync` de solo consulta + numeración atómica en Artículos/Socios de Negocio

## Contexto

`NumeracionDocumentoDetDomain.GenerarCodigoAsync(int serie)` es el endpoint compartido que arma el
código de un documento a partir de una serie (`IniCadena` + `SigNumero` con padding + `FinCadena`).
Hoy, cada llamada incrementa `SigNumero` y lo persiste de inmediato, sin importar si el código
generado se llega a usar en un registro real. El usuario probó esto directamente contra
`api/NumeracionDocumentoDet/GenerarCodigo` y describió el comportamiento esperado con un ejemplo de
4 pasos (serie 5, objeto 3 = Cotizaciones):

1. El número siguiente es, por ejemplo, 1.
2. Al **crear** una cotización con esa serie, el número siguiente pasa a 2.
3. Al **generar** un nuevo código, da 2.
4. Si se **genera** de nuevo sin haber registrado nada con ese 2, sigue dando 2 — no salta a 3.

`CotizacionDomain` (y, por el mismo patrón, `PedidoDomain`/`EntregaDomain`/`FacturaDomain`) ya
implementan exactamente este comportamiento para el número de documento (`NumDoc`): el consecutivo
solo avanza dentro de `InsertarAsync`, en memoria, aprovechando que el repo de
`NumeracionDocumentoDet` y el repo del propio documento comparten el mismo `ApiDbTestContext`
(scoped por request) — el incremento se persiste junto con el INSERT en un solo `SaveChangesAsync`,
sin necesidad de una transacción explícita.

El propio `GenerarCodigoAsync`, sin embargo, nunca se tocó, porque además de usarlo la pantalla
"Numeración de documentos" (donde no importa mucho), lo usan **Artículos** y **Socios de Negocio**
para obtener el valor de su propia clave primaria (`Codigo`, un `string`) *antes* de crear el
registro — ahí el incremento-y-persistencia inmediata funcionaba, en la práctica, como una reserva
del código. Quitarle ese efecto sin más rompería esa reserva.

## Objetivo

- `GenerarCodigoAsync` pasa a ser una consulta pura: nunca incrementa ni persiste `SigNumero`.
- El consecutivo de cualquier serie (Cotizaciones, Pedidos, Entregas, Facturas, Artículos, Socios de
  Negocio) solo avanza cuando el documento/registro correspondiente **se registra de verdad**, nunca
  por el solo hecho de previsualizar/generar un código.
- Artículos y Socios de Negocio mantienen su comportamiento actual de cara al usuario (elegir una
  serie no manual y que el código se genere automáticamente al guardar).

## Fuera de alcance

- No se agrega lógica de "crear a partir del documento anterior" en ningún módulo (ya excluido en el
  spec de Pedido/Entrega/Factura).
- No se agrega bloqueo de fila / transacción explícita a nivel de base de datos para prevenir
  colisiones entre dos altas simultáneas sobre la misma serie no manual — el riesgo de carrera queda
  en el mismo nivel que ya acepta `CotizacionDomain` hoy (ver "Riesgos y trade-offs").
- No se cambia el comportamiento de series manuales.

## Diseño

### 1. `NumeracionDocumentoDetDomain.GenerarCodigoAsync` → solo lectura

Se eliminan las dos líneas que mutan y persisten el consecutivo:

```csharp
linea.SigNumero = linea.SigNumero.Value + 1;
await _repoGenericoNumeracionDocumentoDet.ActualizarAsync(serie, linea);
```

El resto de las validaciones (serie inexistente, bloqueada, sin `SigNumero` configurado, numeración
agotada) se mantiene igual. El método sigue devolviendo el código formateado con el `SigNumero`
*actual*, sin avanzarlo.

### 2. Helper compartido para formatear el código

El cálculo `IniCadena + SigNumero.ToString().PadLeft(CantDigitos, '0') + FinCadena` se necesita ahora
en 4 lugares (el propio `GenerarCodigoAsync` más los tres domains del punto 3). Para no duplicarlo
—la misma clase de duplicación que causó los bugs de comentarios/defaults de Entrega/Factura en la
sesión anterior— se extrae a un método estático:

```csharp
// En NumeracionDocumentoDetDomain, junto al resto de la clase
public static string FormatearCodigo(NumeracionDocumentoDet linea)
{
    var numeroFormateado = linea.SigNumero!.Value.ToString().PadLeft(linea.CantDigitos ?? 0, '0');
    return $"{linea.IniCadena}{numeroFormateado}{linea.FinCadena}";
}
```

`GenerarCodigoAsync` pasa a llamar a este helper en vez de repetir el cálculo inline.

### 3. `ArticuloDomain.InsertarAsync` / `SocioNegocioDomain.InsertarAsync`: mismo patrón que `CotizacionDomain`

Ambas clases reciben una nueva dependencia por constructor:
`IRepositorioGenerico<NumeracionDocumentoDet, int> _repoGenericoNumeracion`.

`InsertarAsync` pasa de:

```csharp
public async Task<bool> InsertarAsync(Articulo obj)
{
    if (await ObtenerPorCodigoAsync(obj.Codigo) != null)
        throw new Exception($"Ya existe un registro con el código: {obj.Codigo}");

    await _repoGenericoArticulo.InsertarAsync(obj);
    return true;
}
```

a (mismo esqueleto para `SocioNegocioDomain`, cambiando el repo genérico y el mensaje):

```csharp
public async Task<bool> InsertarAsync(Articulo obj)
{
    var serie = await _repoGenericoNumeracion.ObtenerAsync(obj.Serie)
        ?? throw new Exception("La serie no existe.");

    if (serie.Bloqueado == "S")
        throw new Exception("La serie está bloqueada y no se puede usar para registrar artículos.");

    if (serie.Manual == "S")
    {
        if (string.IsNullOrWhiteSpace(obj.Codigo))
            throw new Exception("El código es requerido para series manuales.");
    }
    else
    {
        if (serie.SigNumero == null)
            throw new Exception("La serie no tiene configurado el número siguiente.");

        if (serie.FinNumero.HasValue && serie.SigNumero.Value > serie.FinNumero.Value)
            throw new Exception("Se agotó la numeración disponible en esta serie.");

        obj.Codigo = NumeracionDocumentoDetDomain.FormatearCodigo(serie);
        serie.SigNumero = serie.SigNumero.Value + 1;
        // Sin ActualizarAsync explícito -- "serie" ya está rastreada por el mismo DbContext que
        // usa _repoGenericoArticulo; el incremento se persiste junto con el INSERT.
    }

    if (await ObtenerPorCodigoAsync(obj.Codigo) != null)
        throw new Exception($"Ya existe un registro con el código: {obj.Codigo}");

    await _repoGenericoArticulo.InsertarAsync(obj);
    return true;
}
```

El chequeo de duplicado (`ObtenerPorCodigoAsync`) se conserva tal cual y ahora corre también sobre
códigos generados automáticamente (defensivo, cubre el caso borde de una serie mal configurada o una
carrera entre dos altas).

### 4. DTOs

- `ArticuloCrearDTO.Codigo` y `SocioNegocioCrearDTO.Codigo`: se quita `[Required]`, pasan a
  `string?`. La validación de "requerido si la serie es manual" se mueve al Domain (mismo criterio
  que `CotizacionCrearDTO.NumDoc`, que ya es `int?` con la validación de "requerido si es manual"
  dentro de `CotizacionDomain`).
- `ArticuloActualizarDTO`/`SocioNegocioActualizarDTO` no cambian (la edición nunca tocó `Codigo`).

### 5. Web: `ArticulosController`/`SociosNegocioController`, `articulos.js`/`sociosNegocio.js`

- La acción `GenerarCodigoSerie` (ambos controllers) y el `select`/preview del campo `Codigo`
  deshabilitado se **conservan** — siguen mostrando una vista previa del próximo código mientras el
  usuario llena el formulario. La diferencia es que esa vista previa deja de "reservar" nada: si dos
  personas abren el formulario de alta al mismo tiempo, ambas pueden ver el mismo número sugerido
  (igual que ya le pasa hoy a la pantalla "Numeración de documentos").
- Para series **no manuales**, el JS deja de enviar `datos.Codigo` calculado a partir de la vista
  previa; el campo viaja vacío/`null` y el servidor asigna el valor real dentro de `InsertarAsync`.
- Para series **manuales**, no cambia nada (el campo sigue habilitado y su valor viaja tal cual).
- `Crear` (ambos controllers) pasa a re-consultar el registro recién creado y a devolver su `Codigo`
  real en la respuesta JSON — mismo patrón ya usado en `CotizacionesController.Crear` con `numDoc`.
- **Cambio obligatorio, no cosmético, en `sociosNegocio.js`:** la línea `const codigoCreado =
  datos.Codigo;` (línea 445) hoy asume que el código enviado es el que quedó guardado. Con el cambio,
  para series no manuales `datos.Codigo` viaja vacío, así que `codigoCreado` debe tomarse de la
  respuesta del servidor (`respuesta.dato.codigo` o campo equivalente que devuelva `Crear`) antes de
  usarse para crear las direcciones en secuencia. Sin este ajuste, las direcciones creadas después de
  guardar un socio con serie automática quedarían con un `CodigoSn` vacío o incorrecto.

## Riesgos y trade-offs

- **Ventana de colisión en series no manuales:** dos altas casi simultáneas sobre la misma serie
  pueden, en teoría, generar el mismo código si ambas leen `SigNumero` antes de que la primera
  guarde. Es el mismo trade-off que ya existe hoy en `CotizacionDomain`/`PedidoDomain`/etc. (sin
  bloqueo de fila explícito) — el chequeo de duplicado en `InsertarAsync` lo convierte en un error
  claro (`Ya existe un registro con el código: ...`) en vez de un dato corrupto, pero no lo elimina.
  No se pide resolver esto en este cambio.
- **La vista previa del código puede quedar desactualizada** entre que se muestra y que se guarda
  (por la misma razón). Es un problema cosmético, no funcional — el código real siempre se calcula en
  el servidor al momento de guardar.

## Plan de pruebas

- Actualizar las pruebas existentes de `GenerarCodigoAsync` en
  `API.Service.WebApi.Tests/Controllers/NumeracionDocumentoDetControllerTests.cs` (y agregar
  `API.Service.WebApi.Tests/Domain/NumeracionDocumentoDetDomainTests.cs` si no existe ya cobertura a
  nivel Domain) para afirmar que `SigNumero` **no** cambia tras llamar al método dos veces seguidas.
- Nuevos `API.Service.WebApi.Tests/Domain/ArticuloDomainTests.cs` y `SocioNegocioDomainTests.cs`
  (no existen hoy, solo hay Controller tests) cubriendo: serie manual con código provisto, serie
  manual sin código (error), serie automática (código generado y consecutivo avanzado), serie
  bloqueada (error), numeración agotada (error), código duplicado (error).
- Actualizar `ArticuloControllerTests.cs`/`SocioNegocioControllerTests.cs` donde construyan DTOs con
  `Codigo` fijo para una serie no manual, ya que ahora ese valor sería ignorado/sobreescrito.
- `dotnet test` completo en verde antes de dar por terminado.
- Verificación manual (build aislado + navegador) del flujo de alta de Artículo y de Socio de
  Negocio con serie automática, confirmando que el código mostrado tras guardar es el real y que,
  para Socios de Negocio, las direcciones acumuladas quedan con el `CodigoSn` correcto.
