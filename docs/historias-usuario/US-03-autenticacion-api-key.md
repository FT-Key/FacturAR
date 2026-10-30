# US-03 — Autenticación por API key

**Épica:** Integraciones  
**Prioridad:** Alta  
**Estimación:** 1 día  
**Requisito:** RF-03  
**Depende de:** US-02  
**Habilita:** US-05, US-09, US-15, US-16

## Historia

Como sistema cliente, quiero autenticarme con una API key para acceder a los endpoints de negocio de mi empresa sin usar una sesión de portal.

## Contexto

La API key se envía en el header `X-Api-Key`. El backend identifica la key por su prefijo, compara el hash en tiempo constante y resuelve el `ContribuyenteId`. El cliente nunca envía el tenant en el body.

La verificación pública por CAE sigue siendo anónima.

## Criterios de aceptación

- [ ] Los endpoints de negocio exigen el header `X-Api-Key`. — Pendiente: no existen endpoints de negocio (US-05/US-09); solo se validó con un host stub.
- [x] Una key ausente devuelve `401 API_KEY_AUSENTE`.
- [x] Una key mal formada, inexistente, revocada o de una empresa inactiva devuelve `401 API_KEY_INVALIDA`.
- [x] La comparación del hash se hace en tiempo constante.
- [x] Una key válida resuelve el `ContribuyenteId` y lo adjunta al request.
- [x] El contexto de tenant se expone mediante una abstracción de Application.
- [ ] El cliente no puede acceder a datos de otro contribuyente. — Pendiente end-to-end: sin endpoints de comprobantes no hay aislamiento real verificable; solo hay prueba de pipeline con stub.
- [x] Se actualiza `UltimoUso` en cada autenticación exitosa.
- [x] La actualización de `UltimoUso` no bloquea la respuesta si falla.
- [x] La key completa no se registra en logs, errores ni trazas.
- [ ] Los endpoints de emisión y consulta quedan protegidos. — Pendiente: US-05 y US-09 no están implementadas.
- [x] La verificación pública permanece anónima.
- [x] Los errores no revelan si el prefijo existe.
- [ ] Hay tests de 401, 403, aislamiento y actualización de último uso. — Parcial: hay tests de 401 y de último uso; el aislamiento solo está cubierto a nivel de pipeline/stub y `403` no aplica al esquema de API key (no tiene roles).

## Escenarios Gherkin

```gherkin
Scenario: Autenticación exitosa
  Given una API key activa de la empresa "Demo SRL"
  When el cliente envía un request con el header "X-Api-Key"
  Then la API resuelve el contribuyente de la key
  And el endpoint devuelve solo datos de esa empresa
  And se actualiza el último uso de la key
```

```gherkin
Scenario: Sin API key
  Given un cliente envía un request a un endpoint protegido
  When no incluye el header "X-Api-Key"
  Then la API devuelve 401 con código "API_KEY_AUSENTE"
```

```gherkin
Scenario: API key revocada
  Given una API key revocada
  When el cliente la usa en un request
  Then la API devuelve 401 con código "API_KEY_INVALIDA"
  And no se expone si la key existió
```

```gherkin
Scenario: Aislamiento por key
  Given una API key de la empresa A
  When el cliente intenta consultar un comprobante de la empresa B
  Then la API devuelve 404
  And no se devuelven datos de la empresa B
```

```gherkin
Scenario: Verificación pública anónima
  Given un visitante sin API key
  When consulta un CAE existente
  Then la API devuelve 200 con los datos públicos
```

## Tareas técnicas

- [x] Crear `IApiKeyAuthenticator` y `ITenantContext` en Application.
- [x] Implementar `ApiKeyAuthenticationHandler` o middleware en Api.
- [x] Parsear el prefijo y buscar la key en el repositorio.
- [x] Comparar el hash con `CryptographicOperations.FixedTimeEquals`.
- [x] Rechazar keys de contribuyentes inactivos.
- [x] Actualizar `UltimoUso` de forma controlada.
- [ ] Aplicar el esquema de autenticación a los endpoints de negocio. — Pendiente: la política `ApiKeyNegocio` y `RequireApiKey()` existen, pero no hay endpoints de negocio reales que las usen.
- [x] Mantener `/v1/comprobantes/verificar/{cae}` anónimo.
- [x] Agregar tests de autenticación y aislamiento. — Aislamiento cubierto a nivel de pipeline/stub; falta el caso end-to-end con datos reales.
- [x] Documentar el header, errores y ejemplos en el manual.

## Definition of Done

- [ ] Los endpoints protegidos rechazan requests sin key. — Pendiente: no hay endpoints protegidos reales (US-05/US-09).
- [ ] Una key válida solo accede a su empresa. — Pendiente end-to-end por la misma razón.
- [x] La verificación pública sigue funcionando sin autenticación.
- [x] Los logs no contienen keys completas.
- [x] `dotnet test FacturAR.slnx` pasa.
- [x] La historia está enlazada desde el índice y el manual.

## Contrato técnico

Fuente normativa: `docs/contratos-api.md` §4.1 (API key) y §3.3 (aislamiento). Esta sección no se aparta de ese documento.

### Modelo de datos

US-03 **no agrega ni modifica entidades, colecciones ni índices**. Reutiliza lo que define US-02:

| Colección | Documento | Qué usa US-03 |
| --- | --- | --- |
| `apiKeys` | `ApiKey` | `Prefix` (para buscar), `Hash` (para comparar), `Activa`, `ContribuyenteId`, `UltimoUso` (para actualizar). |
| `contribuyentes` | `Contribuyente` | `Id` y `Activo`, para rechazar keys de contribuyentes inactivos. |

Índices usados: `ux_api_keys_prefix` (búsqueda por prefijo) y `_id` (actualización de `UltimoUso`). No hace falta ningún índice nuevo.

El único cambio de estado es `ApiKey.UltimoUso`, que se actualiza con una **actualización condicional** que no necesita índice.

### Autenticación

#### Esquema

Un esquema de autenticación propio de la API, con nombre fijo `ApiKey`, implementado como `AuthenticationHandler<AuthenticationSchemeOptions>`:

```csharp
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = ApiKeyAuthenticationHandler.SchemeName; // "ApiKey"
    options.DefaultChallengeScheme = ApiKeyAuthenticationHandler.SchemeName;
})
.AddCookie("cookies.contribuyente", ...)
.AddCookie("cookies.admin", ...)
.AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", _ => { });
```

El esquema `ApiKey` convive con los dos esquemas de cookie de US-04. **Cada grupo de endpoints declara explícitamente** su autenticación, de modo que una API key nunca habilita un endpoint de sesión y una cookie nunca habilita un endpoint de negocio.

#### Header y formato

```http
X-Api-Key: fak_9f3a1c7b2e40_<secreto>
```

- El nombre del header es configurable con `ApiKeys__HeaderName` (default `X-Api-Key`).
- El formato esperado es exactamente el de US-02: `fak_` + `Prefix` (12 hex) + `_` + `Secret` (43 base64url).
- Expresión regular de validación: `^fak_[0-9a-f]{12}_[A-Za-z0-9_-]{43}$`.
- Longitud máxima admitida del valor del header: `200` caracteres. Un valor más largo se rechaza sin hashear.

#### Flujo de `HandleAuthenticateAsync`

1. Leer el header. Si **no está presente o viene vacío** → `AuthenticateResult.NoResult()` → el challenge responde `401` con `ErrorResponse("API_KEY_AUSENTE", ...)`.
2. Si el valor no cumple la expresión regular → `AuthenticateResult.Fail` → `401` con `ErrorResponse("API_KEY_INVALIDA", ...)`.
3. Extraer el `Prefix` (los 12 hex) y buscar la key con `IApiKeyRepository.GetByPrefixAsync(prefix)`.
4. Si no existe → `401 API_KEY_INVALIDA`.
5. Si `Activa == false` (revocada o regenerada) → `401 API_KEY_INVALIDA`.
6. Calcular `SHA-256` del **valor completo** recibido y compararlo con `ApiKey.Hash` mediante `CryptographicOperations.FixedTimeEquals` (`IApiKeyHasher.Matches`, el mismo de US-02). Si no coincide → `401 API_KEY_INVALIDA`.
7. Cargar el contribuyente con `IContribuyenteRepository.GetByIdAsync(apiKey.ContribuyenteId)`. Si **no existe** o tiene `Activo == false` → `401 API_KEY_INVALIDA`.
8. Construir el principal y devolver `AuthenticateResult.Success`.

#### Respuesta del challenge

`HandleChallengeAsync` devuelve **siempre** `401` con `ErrorResponse` y estos dos códigos:

| Situación | Código | Mensaje |
| --- | --- | --- |
| Header ausente | `API_KEY_AUSENTE` | «Se requiere el header X-Api-Key.» |
| Header presente pero inválido, inexistente, revocado o de empresa inactiva | `API_KEY_INVALIDA` | «La API key no es válida.» |

El mensaje de la fila 2 es **único** para los cuatro casos de fallo. No se distingue entre «el prefijo no existe» y «el hash no coincide»: un atacante no puede usar la respuesta como oráculo para enumerar prefijos.

#### Claims emitidos

| Claim | Valor |
| --- | --- |
| `ClaimTypes.NameIdentifier` | `ApiKey.Id` |
| `contribuyenteId` | `ApiKey.ContribuyenteId` |
| `apikeyId` | `ApiKey.Id` |
| `plano` | `ApiKey` |

Los claims son los mismos que usa la sesión de empresa, así que `ITenantContext` resuelve el tenant de la misma forma en los dos planos.

#### Reglas de seguridad

- La key **completa** no se registra en logs, trazas, `EventoAuditoria`, excepciones ni mensajes de error. Tampoco el `Hash`.
- El handler no loguea el valor del header. Como máximo puede registrar el `Id` de la key y el resultado.
- No se reenvía la key a un servicio externo ni se incluye en un mensaje de `ErrorResponse`.

### Endpoints

US-03 no agrega endpoints propios: **define el esquema de autenticación y lo aplica** a los grupos de endpoints existentes y futuros.

| Grupo de endpoints | Autenticación | Errores |
| --- | --- | --- |
| `/v1/comprobantes/**` (excluyendo `/v1/comprobantes/verificar/{cae}`) | `X-Api-Key` | `401 API_KEY_AUSENTE`, `401 API_KEY_INVALIDA` |
| `/v1/comprobantes/verificar/{cae}` | **Anónimo** | `400 CAE_INVALIDO`, `404 COMPROBANTE_NO_ENCONTRADO` |
| `/v1/soap/**` (US-15, US-16) | `X-Api-Key` | `401 API_KEY_AUSENTE`, `401 API_KEY_INVALIDA` |
| `/v1/api-keys/**` (US-02) | Cookie `cookies.contribuyente` + `EmpresaAdministrador` | `401 CREDENCIALES_INVALIDAS`, `403 SIN_PERMISOS`, `404 API_KEY_INVALIDA` |
| `/v1/auth/**` (US-04) | Cookie `cookies.contribuyente` | `401 CREDENCIALES_INVALIDAS`, `403 SIN_PERMISOS` |
| `/v1/admin/auth/**` (US-04) | Cookie `cookies.admin` | `401 CREDENCIALES_INVALIDAS`, `403 SIN_PERMISOS` |
| `/v1/solicitudes-alta`, `/v1/invitaciones/**` (US-01, US-28) | Anónimo, con su propio rate limit | según la historia correspondiente |
| `/`, `/health`, `/health/live` | Anónimo | — |

Reglas de aplicación:

- Los endpoints de negocio se declaran con `.RequireAuthorization(p => p.AddAuthenticationSchemes("ApiKey").RequireAuthenticatedUser())` o, si se fija `DefaultAuthenticateScheme = "ApiKey"`, con `.RequireAuthorization()` a secas.
- Los endpoints anónimos se declaran con `.AllowAnonymous()` **explícitamente**, aunque el esquema por defecto sea `ApiKey`.
- Los endpoints de cookie se declaran con la política de US-04, que usa `AddAuthenticationSchemes("cookies.contribuyente")` o `("cookies.admin")`. La política reemplaza el esquema por defecto, de modo que la cookie y la API key nunca se mezclan en un mismo endpoint.
- Un endpoint de negocio **no** acepta ninguna forma de credencial alternativa: la API key es la única.

### Casos de uso y servicios

**Application** (`FacturAR.Application`)

| Tipo | Responsabilidad |
| --- | --- |
| `Abstractions.IApiKeyRepository` | Reutilizado de US-02. `GetByPrefixAsync(prefix, ct)` es la única consulta que necesita US-03. |
| `Abstractions.IApiKeyHasher` | Reutilizado de US-02. `Matches(valor, hash)` con `CryptographicOperations.FixedTimeEquals`. |
| `Abstractions.IContribuyenteRepository` | `GetByIdAsync(id, ct)`. Lo agrega US-04; US-03 lo usa para validar el estado del contribuyente. |
| `Abstractions.IApiKeyAuthenticator` | `Task<ResultadoApiKey?> AutenticarAsync(string valorPresentado, CancellationToken ct)`. Devuelve el `ApiKey` autenticado o `null`. Concentra toda la lógica para que el handler de ASP.NET Core sea un adaptador thin. |
| `Abstractions.ITenantContext` | Reutilizado de US-04. `ContribuyenteId` sale del claim `contribuyenteId`. |

**Api** (`FacturAR.Api`)

| Tipo | Responsabilidad |
| --- | --- |
| `Auth/ApiKeyAuthenticationHandler.cs` | Esquema `ApiKey`. Lee el header, delega en `IApiKeyAuthenticator`, arma el principal y escribe el challenge `401` con el código correcto. |
| `Auth/ApiKeyAuthenticationExtensions.cs` | Registra el esquema, los headers y los `Produces` de `401` en OpenAPI. |
| `Auth/ApiKeyUsageRegistrar` | Singleton. Actualiza `UltimoUso` de forma condicional y tolerante a fallos. |

**Infrastructure** (`FacturAR.Infrastructure`)

- `Repositories/ApiKeyRepository` y `Repositories/ContribuyenteRepository` sobre el `MongoDbContext` (singleton). No se agregan colecciones ni índices.

### Autorización

- La API key **no** tiene roles ni scopes en el MVP: habilita todo el grupo de endpoints de negocio de la API, y el aislamiento se hace por tenant.
- La API key **no** habilita `/v1/api-keys`, ni `/v1/auth/**`, ni `/v1/admin/**`: esos exigen cookie de sesión.
- La cookie de sesión **no** habilita `/v1/comprobantes/**`: esos exigen API key. Es deliberado, para que el portal no sea un camino alterno al control de tenant (contrato §4.2).
- La verificación pública por CAE queda fuera de toda autenticación.

### Validaciones

- El header debe estar presente y no vacío.
- El valor debe cumplir `^fak_[0-9a-f]{12}_[A-Za-z0-9_-]{43}$` y medir a lo sumo `200` caracteres.
- El `Prefix` extraído se busca con el índice `ux_api_keys_prefix`.
- El `Hash` almacenado debe ser un hexadecimal de 64 caracteres. Si el documento tiene un `Hash` con formato inesperado, se registra un error en el log y la autenticación falla con `401 API_KEY_INVALIDA` sin exponer el detalle.
- **`ContribuyenteId` nunca se acepta del body ni del query string.** Los DTOs de los endpoints de negocio **no tienen** una propiedad `contribuyenteId`; si el cliente la manda en el JSON, `System.Text.Json` la ignora. Si la manda en el query string, no existe un parámetro que la enlace. El tenant sale **exclusivamente** del claim `contribuyenteId` que escribe el handler a partir de `ApiKey.ContribuyenteId`.
- El estado del contribuyente se valida en cada autenticación: una key de un contribuyente dado de baja deja de autenticar de inmediato, sin necesidad de revocar la key.

### Configuración

| Clave | Tipo | Default | Uso |
| --- | --- | --- | --- |
| `ApiKeys__HeaderName` | `string` | `X-Api-Key` | Nombre del header de autenticación. |
| `ApiKeys__PrefijoFormato` | `string` | `fak_` | Prefijo fijo del formato. Compartido con US-02. |
| `ApiKeys__ActualizarUltimoUso` | `bool` | `true` | Permite desactivar la actualización en ambientes de prueba. |
| `ApiKeys__IntervaloActualizacionUltimoUsoMinutos` | `int` | `1` | Granularidad mínima: no se escribe si la última actualización fue hace menos que esto. |
| `ApiKeys__TimeoutActualizacionUltimoUsoMs` | `int` | `1000` | Tiempo máximo de la actualización en segundo plano. |

No hay secretos en la configuración.

### Integración con otras historias

| Historia | Punto de integración |
| --- | --- |
| US-02 | Define `Prefix`, `Hash`, `IApiKeyRepository` e `IApiKeyHasher`. US-03 no duplica el algoritmo de hash. |
| US-04 | Define `ITenantContext` y los esquemas de cookie. US-03 agrega el esquema `ApiKey` sin tocar los otros dos. |
| US-05, US-09 | Endpoints de emisión y consulta protegidos por `ApiKey`. |
| US-06 | `Idempotency-Key` es un header **ortogonal**: la idempotencia no reemplaza la autenticación y la API key no reemplaza la idempotencia. |
| US-10 | El PDF del comprobante hereda la autenticación del endpoint que lo sirve. |
| US-15, US-16 | SOAP viaja por el mismo pipeline de ASP.NET Core: el cliente SOAP envía el mismo header `X-Api-Key` en la petición HTTP y el tenant se resuelve igual. |
| US-11 | `/v1/comprobantes/verificar/{cae}` queda explícitamente anónimo. |
| US-12, US-13, US-14 | El portal usa la sesión de empresa; no usa API keys para sus llamadas al API. |
| US-18 | La key de demostración del seed se autentica con este mismo esquema. |

## Casos de prueba

| ID | Precondición | Pasos | Resultado esperado |
| --- | --- | --- | --- |
| CP-01 | Una key activa de «Demo SRL» con su contribuyente activo. | `GET` a un endpoint de negocio con el header `X-Api-Key` válido. | `200`. La respuesta contiene **solo** datos de esa empresa. El claim `contribuyenteId` de la sesión corresponde a esa empresa. |
| CP-02 | Cualquier endpoint de negocio protegido. | Llamarlo **sin** el header `X-Api-Key`. | `401` con `codigo = "API_KEY_AUSENTE"`. No se ejecuta la acción ni se consulta Mongo. |
| CP-03 | Cualquier endpoint de negocio protegido. | Enviar `X-Api-Key: no-es-una-key`. | `401` con `codigo = "API_KEY_INVALIDA"`. Mismo mensaje que un hash incorrecto. |
| CP-04 | Ninguna key con el prefijo enviado. | Enviar una key bien formada con un prefijo inexistente. | `401 API_KEY_INVALIDA`. El mensaje es idéntico al de un hash incorrecto: la respuesta no revela si el prefijo existe. |
| CP-05 | Una key revocada (`Activa = false`). | Usar esa key en un endpoint de negocio. | `401 API_KEY_INVALIDA`. La key revocada deja de autenticar de inmediato. |
| CP-06 | Una key de un contribuyente con `Activo = false`. | Usar esa key en un endpoint de negocio. | `401 API_KEY_INVALIDA`. La key es rechazada por el estado del contribuyente, no por el de la key. |
| CP-07 | Una key activa. | Enviar la key con el secreto alterado en un solo carácter. | `401 API_KEY_INVALIDA`. La comparación del hash se hace con `CryptographicOperations.FixedTimeEquals`, sin salida temprana por contenido ni por longitud. |
| CP-08 | Una key activa. | Enviar la key completa correcta. | `AuthenticateResult.Success`. El hash almacenado coincide con el SHA-256 del valor presentado y la comparación en tiempo constante devuelve `true`. |
| CP-09 | Una key de la empresa A. | Consultar un comprobante que pertenece a la empresa B. | `404 COMPROBANTE_NO_ENCONTRADO`. No se devuelven datos de la empresa B. |
| CP-10 | Una key de la empresa A. | Enviar `?contribuyenteId=<id de la empresa B>` en el query string y un body con `"contribuyenteId": "<id de la empresa B>"`. | La petición se resuelve contra la **empresa A**. El tenant sale exclusivamente del claim derivado de la key. Ningún campo del cliente cambia el alcance. |
| CP-11 | Una key usada hace menos de `ApiKeys__IntervaloActualizacionUltimoUsoMinutos` minutos. | Autenticar de nuevo. | `ApiKey.UltimoUso` **no** cambia: la actualización condicional no matchea y no genera una escritura. |
| CP-12 | Una key nunca usada. | Autenticar y consultar el documento. | `ApiKey.UltimoUso` queda informado con la hora UTC de la autenticación. |
| CP-13 | La escritura de `UltimoUso` falla (por ejemplo, la base no responde). | Autenticar con una key válida. | La petición responde `200` con normalidad. El fallo de la actualización no bloquea ni rompe la respuesta; queda registrado como advertencia en el log. |
| CP-14 | Cualquier key. | Revisar los logs, las trazas, la respuesta de error y el evento de auditoría de la petición. | Ni la key completa ni el `Hash` aparecen en ningún logs, traza, respuesta o evento. |
| CP-15 | Un comprobante existente. | `GET /v1/comprobantes/verificar/{cae}` sin header `X-Api-Key`. | `200` con los datos públicos. La verificación permanece anónima. |
| CP-16 | Una sesión de empresa válida. | Llamar a un endpoint de negocio de la API con la cookie de sesión y sin `X-Api-Key`. | `401 API_KEY_AUSENTE`. La cookie del portal no autoriza la API de negocio. |

## Dependencias técnicas

Antes de empezar US-03 tiene que existir:

- **US-02 implementada**: `ApiKey` con `Prefix`, `Hash` y `Activa`; `IApiKeyRepository` con `GetByPrefixAsync`; `IApiKeyHasher` con `Matches`; índice `ux_api_keys_prefix`.
- **US-04 implementada o, como mínimo, su abstracción de tenant**: `ITenantContext` en `Application` y el esquema `cookies.contribuyente` / `cookies.admin`, para que el registro de autenticación tenga los tres esquemas sin colisiones.
- **`IContribuyenteRepository`** en `Application` con `GetByIdAsync`, para validar el estado del contribuyente.
- **Api**: `AddAuthentication` con el esquema `ApiKey` registrado y `AddAuthorization` con las políticas de US-04.
- **Contracts**: no se agregan DTOs. El handler expone los claims, no los cuerpos de respuesta.
- **Tests**: `FacturAR.UnitTests` para `ApiKeyAuthenticationHandler` (con `IApiKeyAuthenticator` y repositorio falsos) y `FacturAR.IntegrationTests` con `MongoFact` para los casos end-to-end, el aislamiento y la actualización de `UltimoUso`.
- **Catálogo de errores**: `docs/contratos-api.md` §2.1 define `API_KEY_AUSENTE` (`401`) y `API_KEY_INVALIDA` (`401`). US-03 los usa tal cual.

## Evidencia de cierre

Fecha de verificación: 2026-10-30. Rama `feature/us-03-autenticacion-api-key`.

| Comando | Resultado |
| --- | --- |
| `dotnet build FacturAR.slnx -c Release` | 0 errores, 0 warnings. |
| `dotnet test FacturAR.slnx -c Release --no-build` | 253/253 unitarios OK; 66 integración OK, 78 omitidos (`MongoFact` sin `MONGO_CONNECTION_STRING`), 0 fallos. |
| `dotnet format FacturAR.slnx --verify-no-changes` | Sin cambios pendientes. |

Tests nuevos y casos concretos:

- `ApiKeyAuthenticatorTests`: formato inválido, valor de más de 200 caracteres, prefijo inexistente, key revocada, hash incorrecto, hash con formato inesperado sin exponer valores en el log, contribuyente inactivo e inexistente, secreto alterado en un carácter, valor vacío y key válida sin loguear el valor.
- `ApiKeyAuthenticationHandlerTests`: sin header → `API_KEY_AUSENTE`; header inválido → `API_KEY_INVALIDA`; header mal formado no consulta el repositorio; fallo al actualizar `UltimoUso` sigue autenticando y registra warning; fallo de infraestructura → `503 MONGO_NO_DISPONIBLE`; claims `NameIdentifier`, `contribuyenteId`, `apikeyId` y `plano`; logs sin el valor ni el hash.
- `ApiKeyUsageRegistrarTests`: actualización desactivada, intervalo respetado, key nunca usada escribe hora UTC, excepción del repositorio no se propaga y deja warning, timeout configurado no rompe la autenticación.
- `ApiKeyStubHostTests` (host de prueba, no endpoint real): anónimo sin key `200`; sin key `401 API_KEY_AUSENTE`; key inválida `401 API_KEY_INVALIDA`; key válida resuelve el contribuyente del claim; `contribuyenteId` en query y body no cambia el tenant; la cookie de sesión no satisface la política; los logs no contienen el valor.
- `ApiKeyPipelineTests` (API real): `/` y `/health/live` sin key `200`; `/v1/comprobantes/verificar/123` sin key `400 CAE_INVALIDO`; login de empresa, login de admin y solicitud de alta siguen llegando a sus endpoints.
- `MongoApiKeyAuthenticationTests` (CP-01, CP-05, CP-06, CP-07, CP-10, CP-11, CP-12, CP-13, CP-14): **omitidos** sin `MONGO_CONNECTION_STRING`.

QA runtime con instancia nueva en `localhost:5199`:

- `GET /health/live` → `200`.
- `GET /` → `200`.
- `GET /v1/comprobantes/verificar/abc` sin header → `400 CAE_INVALIDO` (anónimo, no `401`).
- `GET /v1/auth/me` sin cookie → `401 CREDENCIALES_INVALIDAS` con challenge de `cookies.contribuyente` y `cookies.admin`.
- `GET /v1/comprobantes` → `404` (endpoint de negocio inexistente).
- `GET /v1/comprobantes/verificar/00000000000000` sin header → `500` por Mongo caído; se reproduce igual en la instancia previa `localhost:5171`, donde `/health` responde `503 degraded/database unavailable`. Es un comportamiento preexistente de US-11, no de US-03.

Pendiente por US-05/US-09 y Mongo real:

- Aplicar `RequireApiKey()` y la política `ApiKeyNegocio` a emisión y consultas cuando esos endpoints existan.
- CP-01 end-to-end contra un endpoint de negocio real.
- CP-09 y CP-10 con datos reales: aislamiento contra comprobantes de otra empresa.
- Correr `MongoApiKeyAuthenticationTests` con `MONGO_CONNECTION_STRING`.
- Confirmar en QA que la cookie de sesión no habilita endpoints de negocio reales (hoy solo está cubierto con el stub).

## Notas de alcance

- No implementa OAuth, JWT ni firma HMAC.
- No implementa scopes por endpoint.
- No implementa rotación automática de keys.
- No implementa bloqueo por IP.
