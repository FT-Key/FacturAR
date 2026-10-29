# US-02 — Gestión de API keys

**Épica:** Integraciones  
**Prioridad:** Alta  
**Estimación:** 2 días  
**Requisito:** RF-02  
**Depende de:** US-04, US-28  
**Habilita:** US-03, US-14, US-18

## Historia

Como administrador de una empresa, quiero crear, listar, revocar y regenerar API keys para que mis sistemas externos consuman la API de forma segura y sin compartir mi contraseña.

## Contexto

La API key pertenece a un contribuyente y se administra desde una sesión de empresa con rol `Administrador`. El valor completo se muestra una única vez; la base solo guarda el prefijo visible y el hash.

Formato de referencia:

```text
fak_<prefijo>_<secreto>
```

## Criterios de aceptación

- [ ] Solo un usuario con rol `Administrador` de la empresa puede crear, listar, revocar y regenerar API keys.
- [ ] El `ContribuyenteId` se toma de la sesión autenticada, nunca del body.
- [ ] El nombre de la key es obligatorio, descriptivo y tiene un límite de longitud.
- [ ] Al crear una key se devuelve el valor completo una única vez.
- [ ] La base guarda solo el hash y el prefijo visible.
- [ ] El prefijo es único en el sistema.
- [ ] La key completa tiene suficiente entropía y no se puede adivinar.
- [ ] Listar keys no expone el hash, el secreto ni el valor completo.
- [ ] La lista muestra nombre, prefijo, estado, fecha de creación y último uso.
- [ ] Revocar una key la marca como inactiva y deja de autenticar de inmediato.
- [ ] Regenerar una key invalida la anterior y devuelve el nuevo valor completo una única vez.
- [ ] Un usuario de otra empresa no puede listar, revocar ni regenerar una key ajena.
- [ ] Las operaciones de creación, revocación y regeneración registran auditoría.
- [ ] Se puede tener más de una key activa por empresa.
- [ ] No se permite crear una key para un contribuyente inactivo.
- [ ] La UI muestra el valor completo con una advertencia de copia única.

## Escenarios Gherkin

```gherkin
Scenario: Crear una API key
  Given un administrador autenticado de la empresa "Demo SRL"
  When crea una key llamada "Ecommerce"
  Then la API devuelve el valor completo una única vez
  And la base guarda el hash y el prefijo
  And la lista muestra "Ecommerce" sin el secreto
```

```gherkin
Scenario: Listar keys
  Given una empresa con dos keys activas
  When el administrador abre la sección API keys
  Then ve las dos keys con su prefijo y último uso
  And no ve el secreto ni el hash
```

```gherkin
Scenario: Revocar una key
  Given una key activa de la empresa
  When el administrador la revoca
  Then la key queda inactiva
  And un request con esa key devuelve 401
```

```gherkin
Scenario: Regenerar una key
  Given una key activa de la empresa
  When el administrador la regenera
  Then se devuelve un nuevo valor completo una única vez
  And la key anterior deja de autenticar
  And la nueva key autentica correctamente
```

```gherkin
Scenario: Acceso cruzado
  Given un administrador de la empresa A
  When intenta revocar una key de la empresa B
  Then la API devuelve 404
  And la key de la empresa B sigue activa
```

```gherkin
Scenario: Usuario Consulta no administra keys
  Given un usuario con rol "Consulta"
  When intenta crear una API key
  Then la API devuelve 403 con código "SIN_PERMISOS"
```

## Tareas técnicas

- [ ] Extender `ApiKey` con `RevocadaAt` y `RegeneradaAt` si el modelo lo requiere.
- [ ] Implementar `IApiKeyRepository` en Application.
- [ ] Implementar `CrearApiKeyService`, `ListarApiKeysService`, `RevocarApiKeyService` y `RegenerarApiKeyService`.
- [ ] Generar prefijo y secreto con `RandomNumberGenerator`.
- [ ] Hashear la key con SHA-256 y comparar en tiempo constante en US-03.
- [ ] Crear `POST /v1/api-keys`, `GET /v1/api-keys`, `DELETE /v1/api-keys/{id}` y `POST /v1/api-keys/{id}/regenerar`.
- [ ] Aplicar la política `EmpresaAdministrador`.
- [ ] Crear la sección API keys en el portal.
- [ ] Registrar auditoría.
- [ ] Agregar tests de creación, listado, revocación, regeneración y aislamiento.
- [ ] Documentar endpoints y formato de key.

## Definition of Done

- [ ] El valor completo no se puede recuperar después del alta.
- [ ] La key revocada o regenerada deja de autenticar inmediatamente.
- [ ] El aislamiento entre empresas está cubierto por tests.
- [ ] La sección del portal funciona en desktop y mobile.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el manual.

## Contrato técnico

Fuente normativa: `docs/contratos-api.md` §4.1 (formato de la key, hash, valor único) y §3.3 (aislamiento). Esta sección no se aparta de ese documento.

### Modelo de datos

#### Documento `apiKeys`

`FacturAR.Domain.Entities.ApiKey` ya existe. US-04 lo deja sin cambios en su forma; US-02 **agrega** dos campos:

| Campo | Tipo | Obligatorio | Notas |
| --- | --- | --- | --- |
| `Id` | `Guid` | Sí | Clave primaria. |
| `ContribuyenteId` | `Guid` | Sí | Tenant. **No pasa a nullable**: una API key siempre pertenece a un contribuyente. |
| `Nombre` | `string` | Sí | Descripción para el usuario. 1 a 64 caracteres. |
| `Prefix` | `string` | Sí | **12 caracteres hexadecimales en minúsculas.** Visible para soporte, nunca el secreto. |
| `Hash` | `string` | Sí | SHA-256 en hexadecimal minúsculas (64 caracteres) del **valor completo** de la key. |
| `Activa` | `bool` | Sí | Revocación lógica. |
| `RevocadaAt` | `DateTime?` | No | **Campo nuevo.** UTC. |
| `RegeneradaAt` | `DateTime?` | No | **Campo nuevo.** UTC. |
| `CreatedAt` | `DateTime` | Sí | UTC. |
| `UltimoUso` | `DateTime?` | No | Última autenticación exitosa. Lo actualiza US-03. |

#### Índices

| Índice | Colección | Claves | Tipo | Estado |
| --- | --- | --- | --- | --- |
| `ux_api_keys_prefix` | `apiKeys` | `Prefix: 1` | Único | Ya existe. Es la unicidad **global** del prefijo, no por contribuyente. |

Es el único índice de `apiKeys` en `docs/modelos.md`: el listado por contribuyente se resuelve con el filtro por `ContribuyenteId` de la sesión.

#### Formato exacto de la key

```text
fak_<prefix 12 hex>_<secret 43 base64url>
```

- `fak_` es el prefijo fijo del formato.
- `prefix`: 12 caracteres hexadecimales en minúsculas = 6 bytes de `RandomNumberGenerator.GetBytes(6)` formateados con `Convert.ToHexString(...).ToLowerInvariant()`. Entropía: 48 bits.
- `_`: separador fijo.
- `secret`: 43 caracteres base64url sin padding = 32 bytes de `RandomNumberGenerator.GetBytes(32)` codificados con `Base64UrlEncoder` (`System.Buffers.Text`). Entropía: 256 bits.

Ejemplo de valor completo (solo ilustrativo, el secret es un placeholder):

```text
fak_9f3a1c7b2e40_<secreto>
```

`ApiKey.Prefix` guarda **solo los 12 hex** (`9f3a1c7b2e40`), sin `fak_` y sin el separador. El valor visible al usuario y al soporte es `fak_<Prefix>`, y el valor completo se reconstruye como `fak_` + `Prefix` + `_` + `secret`.

### Autenticación

La gestión de API keys **no** se autentica con API key: se autentica con la **sesión de empresa** (`cookies.contribuyente`) y la política `EmpresaAdministrador` definida en US-04.

- El `ContribuyenteId` sale de `ITenantContext`, nunca del body.
- El body de creación solo acepta `nombre`. Si el cliente manda `contribuyenteId`, la propiedad **no existe** en el DTO y se ignora en silencio.
- El hash y el valor completo nunca viajan en una petición ni en una respuesta que no sea la de creación o regeneración.

#### Hash de la API key

**PBKDF2 no aplica aquí.** El secreto de una API key tiene 256 bits de entropía y se compara por igualdad exacta: el hash se calcula con **SHA-256** y la comparación usa **tiempo constante**.

- `Hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(valorCompleto))).ToLowerInvariant()`.
- Se hashea el **valor completo** (`fak_<prefix>_<secret>`), no solo el secreto.
- La comparación decodifica ambos hex a bytes y usa `CryptographicOperations.FixedTimeEquals`. Los dos hashes son SHA-256, así que siempre miden 32 bytes: `FixedTimeEquals` devuelve `false` ante longitudes distintas sin filtrar información.
- El hasher vive en `Application` (`IApiKeyHasher`) para que US-02 y US-03 usen **exactamente el mismo algoritmo** y no puedan divergir.

El hash es determinístico a propósito: la autenticación necesita recomputar el hash del valor recibido y compararlo con el almacenado. La entropía del secreto compensa el hecho de que SHA-256 no sea una función de password.

#### Valor completo: una sola vez

- `POST /v1/api-keys` y `POST /v1/api-keys/{id}/regenerar` devuelven el valor completo **una única vez**, en el cuerpo de la respuesta.
- `GET /v1/api-keys` devuelve **nunca** el `Hash` ni el valor completo.
- No existe ningún endpoint, método de servicio ni forma de leer el valor completo después de la creación: la base no lo tiene.
- El valor completo **no** se escribe en logs, en auditoría, en `Datos` de `EventoAuditoria` ni en excepciones.

### Endpoints

| Método | Ruta | Autenticación | Body | Respuesta | Errores |
| --- | --- | --- | --- | --- | --- |
| `POST` | `/v1/api-keys` | `cookies.contribuyente` + `EmpresaAdministrador` | `{ "nombre": "Ecommerce" }` | `201 ApiKeyCreadaResponse` (**incluye `valor`**) | `400 VALIDACION`, `401 CREDENCIALES_INVALIDAS`, `403 SIN_PERMISOS`, `403 CUENTA_INACTIVA` |
| `GET` | `/v1/api-keys` | `cookies.contribuyente` + `EmpresaAdministrador` | — | `200 ApiKeyListResponse` | `401 CREDENCIALES_INVALIDAS`, `403 SIN_PERMISOS` |
| `DELETE` | `/v1/api-keys/{id}` | `cookies.contribuyente` + `EmpresaAdministrador` | — | `204` | `401 CREDENCIALES_INVALIDAS`, `403 SIN_PERMISOS`, `404 API_KEY_INVALIDA` |
| `POST` | `/v1/api-keys/{id}/regenerar` | `cookies.contribuyente` + `EmpresaAdministrador` | — | `200 ApiKeyRegeneradaResponse` (**incluye `valor`**) | `401 CREDENCIALES_INVALIDAS`, `403 SIN_PERMISOS`, `404 API_KEY_INVALIDA` |

Cuerpos de respuesta:

```json
{
  "id": "5d4c3b2a-1908-7f6e-5d4c-3b2a19087f6e",
  "nombre": "Ecommerce",
  "prefix": "fak_9f3a1c7b2e40",
  "valor": "fak_9f3a1c7b2e40_<secreto>",
  "createdAt": "2026-10-28T15:30:00Z"
}
```

```json
{
  "items": [
    {
      "id": "5d4c3b2a-1908-7f6e-5d4c-3b2a19087f6e",
      "nombre": "Ecommerce",
      "prefix": "fak_9f3a1c7b2e40",
      "activa": true,
      "createdAt": "2026-10-28T15:30:00Z",
      "ultimoUso": null,
      "revocadaAt": null,
      "regeneradaAt": null
    }
  ]
}
```

El listado **no** contiene `hash`, ni `valor`, ni `secret`: el DTO de listado no tiene esas propiedades.

Una key que no existe para la empresa de la sesión responde `404` con `API_KEY_INVALIDA`: la consulta filtra por `ContribuyenteId` y una key ajena **no existe** para esa sesión, así que el `404` no revela nada.

### Casos de uso y servicios

**Application** (`FacturAR.Application`)

| Tipo | Responsabilidad |
| --- | --- |
| `Abstractions.IApiKeyRepository` | `GetByIdAsync(id, ct)`, `ListByContribuyenteAsync(contribuyenteId, ct)`, `GetByPrefixAsync(prefix, ct)`, `InsertAsync(apiKey, ct)`, `UpdateAsync(apiKey, ct)`. Toda lectura o escritura por id filtra por `ContribuyenteId`. |
| `Abstractions.IApiKeyGenerator` | `ApiKeyGeneratorValue Generar()` → `{ Prefix, Secret, Valor }`. |
| `Abstractions.IApiKeyHasher` | `string Hash(string valor)`, `bool Matches(string valor, string hash)` con `CryptographicOperations.FixedTimeEquals`. |
| `ApiKeys.CrearApiKeyService` | Valida nombre y estado del contribuyente, genera prefix y secret, hashea, inserta y devuelve la respuesta con el valor. |
| `ApiKeys.ListarApiKeysService` | Devuelve el listado del contribuyente de la sesión, sin hash ni valor. |
| `ApiKeys.RevocarApiKeyService` | Marca `Activa = false` y `RevocadaAt = now`. |
| `ApiKeys.RegenerarApiKeyService` | Genera prefix y secret nuevos, reemplaza `Hash`, deja `Activa = true` y fija `RegeneradaAt = now`. |

**Domain**

- `FacturAR.Domain.Entities.ApiKey`: agregar `RevocadaAt` y `RegeneradaAt`.

**Infrastructure** (`FacturAR.Infrastructure`)

- `Repositories/ApiKeyRepository` sobre `MongoDbContext.ApiKeys`.
- `MongoIndexInitializer`: `apiKeys` conserva el índice único `ux_api_keys_prefix`.

**Api** (`FacturAR.Api`)

- `ApiKeys/ApiKeyEndpoints.cs`: los cuatro endpoints, con `.RequireAuthorization(p => p.AddAuthenticationSchemes("cookies.contribuyente").RequireAuthenticatedUser().RequireClaim("rol", "Administrador"))`.

**Web** (`FacturAR.Web`, Blazor Server)

- Sección de API keys del portal: listado, creación, revocación y regeneración.
- Al crear o regenerar, la UI muestra el valor completo **una sola vez**, con una advertencia explícita de copia única y un botón de copiar. Al navegar o recargar, ese valor desaparece y no se puede recuperar.
- La UI no implementa autocomplete de secretos ni los guarda en el navegador.

### Autorización

- Los cuatro endpoints exigen la sesión de empresa (`cookies.contribuyente`) y la política `EmpresaAdministrador` de US-04.
- `Emisor` y `Consulta` reciben `403 SIN_PERMISOS` en los cuatro endpoints. La UI de la sección API keys se oculta para esos roles, pero la autorización real es la del API.
- `SuperAdmin` y `Soporte` **no** administran API keys de empresa: no tienen claim `rol` y reciben `403 SIN_PERMISOS`.
- Toda operación filtra por el `ContribuyenteId` de la sesión. Una key de otra empresa es `404`, nunca `403`: un `403` confirmaría que el id existe.
- Se puede tener más de una key activa por empresa: la unicidad es del prefijo, no del contribuyente.

### Validaciones

- `nombre`: obligatorio, `Trim()`, entre `1` y `64` caracteres, sin caracteres de control. Si falta o no cumple, `400 VALIDACION`.
- `id` de la ruta: debe ser un `Guid` válido; si no lo es, `400 VALIDACION`.
- El contribuyente de la sesión debe estar activo; si no, `403 CUENTA_INACTIVA`.
- No se valida ni acepta `contribuyenteId` en el body o en el query string: no existe en los DTO.
- El prefijo se genera con 6 bytes aleatorios y debe ser **único en el sistema**. Si la inserción choca con `ux_api_keys_prefix`, se genera un prefijo nuevo y se reintenta hasta `5` veces; si el choque persiste, se responde `500 ERROR_INTERNO` y se registra el detalle en el log.
- El `nombre` no es único: dos keys del mismo contribuyente pueden llamarse igual.

### Configuración

| Clave | Tipo | Default | Uso |
| --- | --- | --- | --- |
| `ApiKeys__PrefijoFormato` | `string` | `fak_` | Prefijo fijo del formato de la key. |
| `ApiKeys__LongitudPrefijoHex` | `int` | `12` | Caracteres hexadecimales del prefix. |
| `ApiKeys__BytesSecreto` | `int` | `32` | Bytes aleatorios del secreto (43 caracteres base64url). |
| `ApiKeys__MaxIntentosGeneracionPrefijo` | `int` | `5` | Reintentos ante colisión de prefijo. |

No hay secretos en la configuración: el material de la key se genera en el momento de crearla.

### Integración con otras historias

| Historia | Punto de integración |
| --- | --- |
| US-04 | Define `cookies.contribuyente`, la política `EmpresaAdministrador` y `ITenantContext`, de donde sale el `ContribuyenteId`. |
| US-03 | Consume `Prefix` y `Hash` para autenticar, y usa el mismo `IApiKeyHasher` para comparar en tiempo constante. |
| US-14 | Agrega la gestión de API keys al portal; reutiliza los mismos endpoints. |
| US-17 | Los eventos de auditoría de esta historia se escriben en la colección `eventos`. |
| US-18 | La key de demostración del seed se genera con el mismo `IApiKeyGenerator` y su valor se expone **solo** por la respuesta de creación, nunca por un log ni por el repositorio. |

## Casos de prueba

| ID | Precondición | Pasos | Resultado esperado |
| --- | --- | --- | --- |
| CP-01 | Un `Administrador` autenticado de la empresa «Demo SRL» con contribuyente activo. | `POST /v1/api-keys` con `{ "nombre": "Ecommerce" }`. | `201` con el valor completo `fak_<12 hex>_<43 base64url>`. En la base quedan `Prefix` (12 hex) y `Hash` (SHA-256 hex de 64 caracteres). El valor completo no queda en la base. |
| CP-02 | El mismo administrador. | `GET /v1/api-keys`. | `200` con el listado. La respuesta **no** contiene `hash`, ni `valor`, ni `secret`: el DTO de listado no tiene esas propiedades. |
| CP-03 | Una key creada en CP-01. | Inspeccionar el documento almacenado y las respuestas de `GET`, `DELETE` y `regenerar`. | El `Hash` nunca aparece en ninguna respuesta de la API. Solo `Prefix` es visible. |
| CP-04 | Una key creada en CP-01. | Crear la key y volver a listar, recargar la página y reintentar la creación con el mismo nombre. | El valor completo solo aparece en la respuesta de creación. Ninguna otra operación ni pantalla lo vuelve a mostrar. No hay forma de recuperarlo. |
| CP-05 | Una key activa. | `DELETE /v1/api-keys/{id}` y luego usar esa key en un endpoint de negocio. | `204`. La key queda con `Activa = false` y `RevocadaAt` informado. La petición con esa key responde `401 API_KEY_INVALIDA`. |
| CP-06 | Una key activa. | `POST /v1/api-keys/{id}/regenerar`. | `200` con un valor completo **nuevo**. La key anterior deja de autenticar (`401 API_KEY_INVALIDA`) y la nueva autentica correctamente. El documento conserva `Id` y `Nombre`, y registra `RegeneradaAt`. |
| CP-07 | Un usuario con rol `Consulta` autenticado. | `POST /v1/api-keys`. | `403 SIN_PERMISOS`. No se crea ninguna key. |
| CP-08 | Un `Administrador` de la empresa A. | `DELETE /v1/api-keys/{id}` con el id de una key de la empresa B. | `404 API_KEY_INVALIDA`. La key de la empresa B sigue activa. El `403` no se usa: no se revela la existencia del recurso. |
| CP-09 | Un `Administrador` autenticado. | `POST /v1/api-keys` con `{ "nombre": "" }` y con `{ }`. | `400 VALIDACION` en ambos casos. No se crea ninguna key. |
| CP-10 | El contribuyente de la sesión está `Activo = false`. | `POST /v1/api-keys`. | `403 CUENTA_INACTIVA`. No se crea ninguna key. |
| CP-11 | Un `Administrador` autenticado. | Crear varias keys en forma repetida. | Todos los prefijos son distintos entre sí y de los ya existentes. La unicidad la garantiza `ux_api_keys_prefix`. |
| CP-12 | Una key creada. | Comparar el hash almacenado con el SHA-256 del valor completo usando `CryptographicOperations.FixedTimeEquals`. | La comparación da `true` con el valor completo y `false` con cualquier variante. La comparación se hace sobre bytes, en tiempo constante, sin salida temprana por longitud ni por contenido. |
| CP-13 | Un `Administrador` autenticado. | Crear, revocar y regenerar una key. | Cada operación deja un evento en la colección `eventos` con la acción correspondiente. El `Datos` del evento no contiene el hash, el secreto ni el valor completo. |
| CP-14 | Un `Administrador` autenticado. | Crear tres keys distintas. | Las tres quedan activas en simultáneo. El listado devuelve las tres. |
| CP-15 | Un `SuperAdmin` autenticado en la consola global. | `GET /v1/api-keys`. | `403 SIN_PERMISOS`. La consola global no administra API keys de empresa. |

## Dependencias técnicas

Antes de empezar US-02 tiene que existir:

- **US-04 implementada**: cookie `cookies.contribuyente`, política `EmpresaAdministrador`, `ITenantContext` y `FacturAR.Application.Abstractions.IUsuarioRepository` / `IContribuyenteRepository` para validar el estado del contribuyente.
- **Domain**: `FacturAR.Domain.Entities.ApiKey` con `RevocadaAt` y `RegeneradaAt` agregados.
- **Application**: `IApiKeyRepository`, `IApiKeyGenerator`, `IApiKeyHasher` y los servicios `CrearApiKeyService`, `ListarApiKeysService`, `RevocarApiKeyService`, `RegenerarApiKeyService`. `FacturAR.Application` no referencia `MongoDB.Driver`.
- **Infrastructure**: `MongoDbContext.ApiKeys` (ya existe) y `ApiKeyRepository`; `MongoIndexInitializer` conserva el índice único `ux_api_keys_prefix`.
- **Contracts**: `FacturAR.Contracts.ApiKeys.ApiKeyCreadaResponse`, `ApiKeyRegeneradaResponse`, `ApiKeyResumenResponse` y `ApiKeyListResponse`, más `CrearApiKeyRequest`. Ningún DTO de listado expone `Hash`.
- **Api**: registro de `IApiKeyRepository`, `IApiKeyGenerator` e `IApiKeyHasher` como `Scoped`, y los cuatro endpoints con la política `EmpresaAdministrador`.
- **Web**: sección de API keys del portal con la advertencia de copia única.
- **Tests**: `FacturAR.UnitTests` para el generador y el hasher; `FacturAR.IntegrationTests` con `MongoFact` para creación, listado, revocación, regeneración y aislamiento.
- **Documentación**: `docs/contratos-api.md` §14 ya reserva los cuatro endpoints y §2.1 el catálogo de errores: US-02 no agrega códigos nuevos.

## Notas de alcance

- No implementa scopes ni permisos granulares por key.
- No implementa vencimiento automático de keys.
- No implementa autenticación con la key; eso corresponde a US-03.
- No se envían keys por email.
