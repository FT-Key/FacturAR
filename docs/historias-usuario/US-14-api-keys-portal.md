# US-14 — API keys desde el portal

**Épica:** Portal  
**Prioridad:** Media  
**Estimación:** 1 día  
**Requisito:** RF-14  
**Depende de:** US-02, US-04, US-28  
**Habilita:** US-21, US-30

## Historia

Como administrador de una empresa, quiero administrar mis API keys desde el portal para crear integraciones sin usar la API ni compartir mi contraseña.

## Contexto

La sección API keys consume los endpoints de US-02. El valor completo se muestra una sola vez y no se vuelve a solicitar al servidor.

## Criterios de aceptación

- [ ] Solo un usuario `Administrador` puede acceder a la sección.
- [ ] La sección lista keys con nombre, prefijo, estado, creación y último uso.
- [ ] Se puede crear una key con nombre.
- [ ] Al crear, se muestra el valor completo una única vez con una advertencia.
- [ ] Se puede copiar el valor al portapapeles.
- [ ] Al cerrar el modal, el valor deja de mostrarse y no se puede recuperar.
- [ ] Se puede revocar una key con confirmación.
- [ ] Se puede regenerar una key con confirmación y se muestra el nuevo valor una vez.
- [ ] La lista se actualiza después de crear, revocar o regenerar.
- [ ] Un usuario de otra empresa no puede ver ni modificar keys ajenas.
- [ ] La UI muestra estados de carga, vacío y error.
- [ ] La sección es responsive y accesible por teclado.
- [ ] Hay una prueba manual del flujo completo.

## Escenarios Gherkin

```gherkin
Scenario: Crear key desde el portal
  Given un administrador autenticado
  When crea una key llamada "Ecommerce"
  Then ve el valor completo una única vez
  And la lista muestra la key con su prefijo
```

```gherkin
Scenario: Copiar key
  Given una key recién creada
  When el administrador pulsa "Copiar"
  Then el valor se copia al portapapeles
  And el portal muestra una confirmación
```

```gherkin
Scenario: Cerrar el modal
  Given una key recién creada visible
  When el administrador cierra el modal
  Then el valor completo ya no se muestra
  And no hay forma de recuperarlo desde la lista
```

```gherkin
Scenario: Revocar key
  Given una key activa
  When el administrador confirma la revocación
  Then la key aparece como revocada
  And deja de autenticar en la API
```

```gherkin
Scenario: Usuario Consulta
  Given un usuario con rol "Consulta"
  When intenta abrir la sección API keys
  Then el portal le muestra acceso denegado
```

## Tareas técnicas

- [ ] Crear la página de API keys en el portal.
- [ ] Integrar listado, creación, revocación y regeneración.
- [ ] Crear el modal de valor único con botón de copiar.
- [ ] Agregar confirmaciones de revocación y regeneración.
- [ ] Aplicar autorización por rol.
- [ ] Agregar estados de carga, vacío y error.
- [ ] Agregar prueba manual y, si es posible, test de integración del endpoint.
- [ ] Enlazar el manual de integración.

## Definition of Done

- [ ] El flujo crear → copiar → revocar funciona desde el portal.
- [ ] El valor completo no se puede recuperar después del alta.
- [ ] La sección está aislada por empresa y rol.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el manual.

## Contrato técnico

Fuente normativa: `docs/contratos-api.md` (§2 errores, §3 roles, §4.1 API key, §14 convenciones de API) y `docs/modelos.md` (entidad `ApiKey`). Esta sección no se aparta de esos documentos.

### Modelo de datos

US-14 **no agrega colecciones, entidades ni índices**. Es una historia de portal sobre datos que ya existen.

| Colección | Documento | Qué usa US-14 |
| --- | --- | --- |
| `apiKeys` | `ApiKey` | `Nombre`, `Prefix`, `Activa`, `RevocadaAt`, `RegeneradaAt`, `CreatedAt`, `UltimoUso`. |

- El índice `ux_api_keys_prefix` (único) y el filtro por `ContribuyenteId` los define US-02. US-14 no los toca.
- El `Hash` **no se pide, no se muestra y no viaja al navegador**. El listado usa `ApiKeyResumenResponse`, que no tiene esa propiedad.
- US-14 no escribe en MongoDB: toda alta, revocación y regeneración pasa por la API de US-02.

### Endpoints

US-14 **no agrega endpoints**: consume los cuatro de US-02, que ya están autorizados con la cookie de empresa y la política `EmpresaAdministrador`.

| Método | Ruta | Autenticación | Body | Respuesta | Errores |
| --- | --- | --- | --- | --- | --- |
| `GET` | `/v1/api-keys` | `cookies.contribuyente` + `EmpresaAdministrador` | — | `200` `ApiKeyListResponse` | `401 CREDENCIALES_INVALIDAS`, `403 SIN_PERMISOS`, `503 MONGO_NO_DISPONIBLE`, `500 ERROR_INTERNO` |
| `POST` | `/v1/api-keys` | Ídem | `{ "nombre": "Ecommerce" }` | `201` `ApiKeyCreadaResponse` (**incluye `valor`**) | `400 VALIDACION`, `401 CREDENCIALES_INVALIDAS`, `403 SIN_PERMISOS`, `403 CUENTA_INACTIVA` |
| `DELETE` | `/v1/api-keys/{id:guid}` | Ídem | — | `204` | `401 CREDENCIALES_INVALIDAS`, `403 SIN_PERMISOS`, `404 API_KEY_INVALIDA` |
| `POST` | `/v1/api-keys/{id:guid}/regenerar` | Ídem | — | `200` `ApiKeyRegeneradaResponse` (**incluye `valor`**) | `401 CREDENCIALES_INVALIDAS`, `403 SIN_PERMISOS`, `404 API_KEY_INVALIDA` |

- El `404 API_KEY_INVALIDA` de la revocación y la regeneración es el de US-02: una key de otra empresa **no existe** para esta sesión y la respuesta no revela nada.
- El valor completo (`fak_<prefix>_<secret>`) viaja **solo** en la respuesta de creación y en la de regeneración. No hay ningún endpoint que lo devuelva después.

### Casos de uso y servicios

**Web** (`FacturAR.Web`)

| Tipo | Responsabilidad |
| --- | --- |
| `Services/FacturarApiClient.ListarApiKeysAsync(ct)` | `GET /v1/api-keys`. |
| `Services/FacturarApiClient.CrearApiKeyAsync(nombre, ct)` | `POST /v1/api-keys`. |
| `Services/FacturarApiClient.RevocarApiKeyAsync(id, ct)` | `DELETE /v1/api-keys/{id}`. |
| `Services/FacturarApiClient.RegenerarApiKeyAsync(id, ct)` | `POST /v1/api-keys/{id}/regenerar`. |
| `Services/ApiKeyValorState` | Estado en memoria con el valor a mostrar una vez. Vive en el circuito Blazor: **no** se persiste en `localStorage`, `sessionStorage`, cookies ni en la URL. |

El `FacturarApiClient` reenvía el header `Cookie` de la petición entrante (US-04) y usa el `CancellationToken` del circuito.

El portal **no** implementa hashing, generación de prefijos ni revocación: todo eso es de US-02. El portal solo orchestrá la llamada, muestra el resultado y aplica la regla de «una sola vez».

### Validaciones

- Nombre de la key: obligatorio, entre 1 y 100 caracteres, sin espacios al principio ni al final. Si falta, la API responde `400 VALIDACION` y el portal muestra el error junto al campo.
- El prefijo que se muestra es `fak_<12 hex>` (`ApiKeyResumenResponse.prefix`). El secreto nunca se muestra en el listado.
- La revocación y la regeneración **exigen confirmación** en un diálogo. El botón destructivo se distingue con texto y no solo con color.
- Tras una revocación o una regeneración, el listado se vuelve a pedir a la API: la lista no se modifica a mano.
- Un `404 API_KEY_INVALIDA` se muestra como «La key no existe o no pertenece a tu empresa», sin más detalle.
- Un `403 SIN_PERMISOS` se muestra como «Acceso denegado» y la sección queda inaccesible.

### UI: rutas del portal, componentes y estados

| Ruta | Archivo | Contenido |
| --- | --- | --- |
| `/api-keys` | `Components/Pages/ApiKeys.razor` | Listado, alta, revocación y regeneración. |

Componentes compartidos en `Components/Shared/`:

| Componente | Uso |
| --- | --- |
| `ApiKeyValorModal.razor` | Modal de valor único: muestra `fak_<prefix>_<secret>`, un botón «Copiar» y el aviso de que no vuelve a mostrarse. |
| `ConfirmDialog.razor` | Confirmación de revocación y de regeneración. |
| `ApiKeyEstadoBadge.razor` | Distintivo «Activa» / «Revocada», con texto además de color. |

Reglas del modal de valor único:

- **El valor se muestra una sola vez.** El estado vive en el circuito Blazor (`ApiKeyValorState`). Al cerrar el modal, el valor se descarta: no queda en el DOM, ni en el historial del navegador, ni en el estado de la lista.
- **Nunca se persiste en el navegador**: nada de `localStorage`, `sessionStorage`, cookies ni `data-*` en el HTML. Tampoco se escribe en la consola del navegador.
- Botón «Copiar»: usa `navigator.clipboard.writeText`. Requiere contexto seguro (HTTPS o `localhost`); en un entorno sin contexto seguro el portal muestra el valor con un aviso para copiarlo a mano. Después de copiar, muestra una confirmación visible («Copiado») y devuelve el foco al botón.
- Accesibilidad del modal: `role="dialog"`, `aria-modal="true"`, `aria-labelledby` con el título, foco inicial en el botón «Copiar», foco atrapado dentro del modal, `Esc` para cerrar y devolución del foco al elemento que lo abrió.
- El valor se muestra en un elemento de solo lectura con `select` para poder seleccionarlo, y con `aria-label="Valor de la API key"`. No se copia automáticamente al portapapeles sin que el usuario lo pida.

Estados de la pantalla:

| Estado | Comportamiento |
| --- | --- |
| Carga | Indicador con `aria-busy="true"` mientras se pide el listado. |
| Con datos | Tabla con nombre, prefijo, estado, creación y último uso. |
| Vacío | «Todavía no creaste API keys» con un botón «Crear API key» y una explicación de para qué sirve. |
| Error | Mensaje con el código devuelto y botón «Reintentar». |
| Acción en curso | El botón de la fila Accionada queda deshabilitado mientras corre la llamada, para no disparar dos veces. |

- El listado **no** muestra el `Hash` ni el valor: solo `fak_<prefix>`, el estado, `CreatedAt`, `UltimoUso`, `RevocadaAt` y `RegeneradaAt`.
- La sección es responsive (tabla con `table-responsive`, sin scroll horizontal obligatorio en móvil) y navegable por teclado: enlaces y botones con foco visible y `aria-label` en los botones de icono.
- El enlace «API keys» del menú solo aparece para el rol `Administrador`. Un rol `Consulta` o `Emisor` que abra `/api-keys` recibe `403 SIN_PERMISOS` y ve «Acceso denegado».

### Integración con otras historias

| Historia | Punto de integración |
| --- | --- |
| US-02 | Los cuatro endpoints, `ApiKeyResumenResponse`, `ApiKeyCreadaResponse` y `ApiKeyRegeneradaResponse`, y la regla de mostrar el valor una sola vez. |
| US-03 | La key creada desde el portal sirve para autenticarse contra la API de negocio con el header `X-Api-Key`. |
| US-04 | Cookie `cookies.contribuyente`, claim `contribuyenteId` y política `EmpresaAdministrador`. |
| US-12 | El menú del portal; el enlace «API keys» solo para `Administrador`. |
| US-30 | El manual de integración publica la guía de creación de keys; US-14 y US-30 deben usar el mismo texto sobre el valor único. |
| US-21 | Prueba manual del flujo crear → copiar → revocar y actualización de la colección Postman si cambian los endpoints. |

## Casos de prueba

| ID | Precondición | Pasos | Resultado esperado |
| --- | --- | --- | --- |
| CP-01 | Administrador autenticado de una empresa sin keys. | Abrir `/api-keys`. | Estado vacío con la explicación y el botón «Crear API key». |
| CP-02 | Administrador autenticado. | Crear una key llamada «Ecommerce». | Se abre el modal con el valor completo `fak_<12 hex>_<43 base64url>` y el aviso de que no vuelve a mostrarse. El listado muestra la key con su prefijo `fak_<prefix>`. |
| CP-03 | Key recién creada. | Pulsar «Copiar». | El valor se copia al portapapeles y el portal muestra la confirmación «Copiado». |
| CP-04 | Key recién creada visible. | Cerrar el modal. | El valor deja de mostrarse. El listado no lo tiene y no hay forma de recuperarlo desde el portal. |
| CP-05 | Cualquiera. | Inspeccionar `localStorage`, `sessionStorage`, las cookies y el HTML de la página. | El valor completo de la key no aparece en ninguno de ellos. |
| CP-06 | Key activa. | Revocarla y confirmar. | La key aparece como revocada en el listado y deja de autenticar: `GET /v1/comprobantes` con esa key responde `401 API_KEY_INVALIDA`. |
| CP-07 | Key activa. | Regenerarla y confirmar. | Se muestra el nuevo valor una sola vez. La key anterior deja de autenticar de inmediato. |
| CP-08 | Key activa. | Abrir el diálogo de revocación y cancelarlo. | No se ejecuta ninguna llamada: la key sigue activa. |
| CP-09 | Usuario con rol `Consulta`. | Abrir `/api-keys`. | El portal muestra «Acceso denegado» y la API responde `403 SIN_PERMISOS`. El enlace no aparece en el menú. |
| CP-10 | Usuario con rol `Emisor`. | Enviar a mano un `GET /v1/api-keys` con la cookie de la sesión. | `403 SIN_PERMISOS`. No se lista ninguna key. |
| CP-11 | API caída. | Abrir `/api-keys`. | Estado de error con botón «Reintentar». No se muestra un listado vacío que parezca «no tenés keys». |
| CP-12 | Listado con varias keys. | Crear una key y volver al listado. | El listado se actualiza con la key nueva y el estado vacío ya no aparece. |
| CP-13 | Cualquier listado. | Inspeccionar el cuerpo de `GET /v1/api-keys`. | No contiene `hash`, ni `valor`, ni `secret`. Solo `prefix`, `activa`, `createdAt`, `ultimoUso`, `revocadaAt` y `regeneradaAt`. |
| CP-14 | Modo sin contexto seguro (http en un host que no sea `localhost`). | Crear una key y pulsar «Copiar». | El portal avisa que no puede copiar automáticamente y ofrece el valor para copiarlo a mano. La creación sigue siendo correcta. |
| CP-15 | Administrador autenticado. | Recorrer `/api-keys` solo con el teclado. | El modal atrapa el foco, `Esc` lo cierra, el foco vuelve al botón que lo abrió y todos los controles tienen foco visible. |

## Dependencias técnicas

Antes de empezar US-14 tiene que existir:

- **US-02 implementada**: los cuatro endpoints de `/v1/api-keys` con la política `EmpresaAdministrador`, `IApiKeyRepository`, `IApiKeyGenerator`, `IApiKeyHasher` y los servicios de creación, listado, revocación y regeneración.
- **US-03 implementada**: el esquema de autenticación `ApiKey`, para que la key creada desde el portal pueda usarse contra la API de negocio y para verificar que una key revocada deja de autenticar.
- **US-04 implementada**: cookie `cookies.contribuyente`, claim `contribuyenteId`, política `EmpresaAdministrador` y `FacturarApiClient` reenviando la cookie.
- **Contracts**: `ApiKeyResumenResponse`, `ApiKeyListResponse`, `ApiKeyCreadaResponse`, `ApiKeyRegeneradaResponse` y `CrearApiKeyRequest` (US-02).
- **Web**: `Components/Pages/ApiKeys.razor`, `Components/Shared/ApiKeyValorModal.razor`, `Components/Shared/ConfirmDialog.razor`, `Components/Shared/ApiKeyEstadoBadge.razor`, `ApiKeyValorState` y los cuatro métodos de `FacturarApiClient`.
- **Tests**: `FacturAR.IntegrationTests` con `MongoFact` para el aislamiento por empresa y la revocación; prueba manual del flujo completo crear → copiar → revocar, registrada en la historia.
- **Documentación**: `docs/manual-integracion.md` §3 y `docs/manual-integracion.md` en línea (US-30) con el mismo texto sobre el valor único, y la colección Postman.

## Notas de alcance

- No implementa scopes ni permisos por key.
- No implementa historial de uso detallado.
- No implementa envío de keys por email.
