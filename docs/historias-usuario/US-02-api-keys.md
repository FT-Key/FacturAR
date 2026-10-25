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
  Then la API devuelve 403 con código "SIN_PERMISO"
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

## Notas de alcance

- No implementa scopes ni permisos granulares por key.
- No implementa vencimiento automático de keys.
- No implementa autenticación con la key; eso corresponde a US-03.
- No se envían keys por email.
