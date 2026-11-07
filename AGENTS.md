# AGENTS.md — API de GESCOM

Guía para agentes de IA (y personas) que trabajan en este repositorio. Mantenla corta y actualizada. Estado y pendientes: `docs/traspaso-2026-11-07.md`.

## Qué es
API REST del sistema de gestión GESCOM (ventas, compras, inventario multi-almacén, socios de negocio, cuentas por cobrar/pagar, taller). .NET 7, SQL Server, JWT. La Web (.NET 8 MVC) vive en el repo hermano `Web` y consume esta API. Referencia funcional: SAP Business One.

## Arquitectura (N-capas)
`Service.WebApi` (controladores) → `Application.Main` (casos de uso, DTO, AutoMapper) → `Domain.Core` (reglas de negocio) → `Infraestructure.Repository` (EF Core 7, DB-first).
- **Regla de capas:** `Application.*` depende solo de `Domain.Interface`, nunca de `Domain.Core`. Los tipos compartidos entre capas (records, interfaces, utilidades puras) van en `Domain.Interface`.
- Entidades `partial` en `Domain.Entity/Models` (+ `ApiDbTestContext`); mapeos en `Transversal.Mapper/PerfilMapeo.cs`. AutoMapper **no** debe poblar colecciones/navegaciones que el dominio inserta explícitamente (usar `Ignore()`).
- Registro de DI en `Service.WebApi/Startup.cs`.
- Repositorio genérico `IRepositorioGenerico<T, TKey>`: `ActualizarAsync` copia solo columnas no clave y llama `SaveChanges` por sí mismo; `AgregarSinGuardarAsync` adjunta sin guardar. Para operaciones atómicas usar `IEjecutorTransaccion.EjecutarAsync` (patrón `PermisoDomain.GuardarMatriz*`).
- Respuestas siempre `Respuesta<T> { Resultado, Mensaje, Dato }`. La capa Application atrapa excepciones de negocio y devuelve `Resultado = false` con `ex.Message`; el controlador responde 400 (o 404 si el mensaje lo indica, p. ej. «El artículo X no existe»). 409 solo para crédito excedido (`RespuestaCredito<T>`).
- Permisos: `[RequierePermiso("Modulo", Accion.Ver|Agregar|Actualizar|Eliminar)]` en cada acción; módulos en tabla `Modulo`, asignados por plantillas/usuario. El superusuario `admin` los tiene todos.
- Evita parámetros opcionales en interfaces de dominio (rompen Moq con CS0854).

## Reglas de negocio clave (no las rompas)
- **Documentos de venta:** Cotización(3) → Pedido(4) → Entrega(5) → Factura(6); orden de servicio(70). Las líneas enlazan con `BaseTipo/BaseEntry/BaseLinea` (`DocumentoBaseService`). Compras análogas (factura de compra = 13). Notas de crédito/débito.
- **Numeración:** los dominios validan **antes** de numerar; un rechazo no consume consecutivo (serie `SigNumero`).
- **Inventario:** todo movimiento de stock pasa por `InventarioAsientoService.AplicarMovimientoAsync` (único punto). Las reversas (`RevertirAsync/RevertirLineaAsync`) nunca se bloquean. Existencias por artículo+almacén en `ExistenciaArticulo` (`Disponible` = en stock; `Comprometido`/`Pedido` hoy no los mantiene ningún documento).
- **Stock por almacén (`ArticuloAlmacen`):** mínimo, máximo (0 = sin valor, tope 9,999,999,999,999.999999) y `Bloqueado`; un artículo bloqueado en un almacén rechaza entradas y salidas. `PUT api/Articulo/{codigo}/Almacenes` **reemplaza todo**; `[]` borra la configuración, cuerpo nulo = 400.
- **Cuentas por cobrar/pagar:** pagos recibidos/efectuados, condiciones de pago y vencimientos, estado de cuenta, antigüedad de saldos, límite de crédito por cliente (`NULL` = sin límite, `0` = solo contado; se verifica en pedido/entrega/factura; exceso → 409 y se autoriza con el permiso `AutorizarExcesoCredito`).
- El `PUT` de socio reemplaza **todas** las columnas escalares: el cliente debe enviar siempre los valores actuales.

## Comandos
```powershell
dotnet build API.sln
dotnet test API.Service.WebApi.Tests --nologo -v q        # línea base: 2713 pasando, 0 fallando
dotnet test API.Service.WebApi.Tests --filter "FullyQualifiedName~NombreClase"
```
Si hay una API/Web en ejecución que bloquea `bin`, compila/prueba con `-p:BaseOutputPath="<carpeta temporal>\"` (los errores MSB3021/MSB3027 son de bloqueo de archivos, no de compilación).

## Base de datos
- Los scripts de `sql/` son **idempotentes** y van por fecha. `crear-api-db-test-completo.sql` (y `crear-api-db-completo.sql`, sin seguimiento en git) crea la base **desde cero** con datos iniciales; al agregar un cambio de esquema se agrega el mismo bloque al final de ese script.
- En `CHECK`/`DEFAULT` que referencian una columna agregada en el mismo lote hay que usar `EXEC('...')`.
- **Pruebas con SQL real:** solo en bases temporales `API_DB_EVID<n>` creadas con el script completo y eliminadas al terminar. **Jamás** contra `API_DB_TEST` ni una base del usuario.
- La contraseña de `sa` y la clave JWT están en `appsettings.json` (local). **Nunca** se imprimen, se copian a otros archivos ni se commitean. Alternativa recomendada: variables de entorno `ConnectionStrings__API_DB` y `Jwt__Key`.

## Git
- Rama de trabajo: `desarrollo` (la rama `main` no se toca sin pedirlo).
- Commits con rutas explícitas (`git add <archivo>`), **nunca** `git add -A` / `git add .`. **Nunca** commitear `API.Service.WebApi/appsettings.json` ni los `sql/crear-api-db-completo*.sql` locales.
- **No hacer `push` salvo que el usuario lo pida.**
- Mensajes: `tipo(modulo): descripcion` en español, sin tildes (`feat`, `fix`, `docs`). Conservar el fin de línea de cada archivo (el repo usa autocrlf): no reformatear.

## Cómo trabajar
- Antes de cambiar un módulo, lee su spec en `docs/superpowers/specs/` (y el plan en `docs/superpowers/plans/`). El flujo usado: diseño (spec) → plan por tareas → implementación con pruebas (xUnit + Moq) → prueba contra SQL real → recorrido en navegador.
- TDD: prueba primero y verla fallar. Cada tarea cierra con build sin errores y suite completa en verde.
- Los mensajes de error de negocio son literales en español y las pruebas los verifican: no los cambies sin actualizar sus pruebas.
- Hallazgos fuera de alcance: anótalos, no los mezcles en el mismo commit.
