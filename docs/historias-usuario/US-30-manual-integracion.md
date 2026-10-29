# US-30 — Manual online de integración

**Épica:** Portal  
**Prioridad:** Media  
**Estimación:** 1 día  
**Requisito:** RF-27  
**Depende de:** US-02, US-03, US-05  
**Habilita:** US-21

## Historia

Como desarrollador de una empresa cliente, quiero leer un manual online claro y actualizado para saber cómo solicitar acceso, activar mi cuenta, crear una API key, autenticar requests y usar la API en mis proyectos.

## Contexto

El manual debe ser público o accesible desde el portal y estar enlazado desde la home, el login y el panel de API keys. No debe duplicar información desactualizada: los ejemplos y errores deben coincidir con OpenAPI y Postman.

## Criterios de aceptación

- [ ] Existe una página `/manual` accesible sin autenticación.
- [ ] El manual explica el proceso de solicitud, aprobación y activación.
- [ ] El manual explica cómo crear, listar, revocar y regenerar una API key.
- [ ] El manual explica el header `X-Api-Key` y el formato de la key.
- [ ] El manual incluye ejemplos de emisión en `curl`, JavaScript y C#.
- [ ] El manual incluye ejemplos de consulta y verificación pública.
- [ ] El manual explica la idempotencia y el header `Idempotency-Key`.
- [ ] El manual lista los códigos de error y su significado.
- [ ] El manual incluye buenas prácticas de seguridad.
- [ ] El manual aclara que el CAE es simulado y no tiene validez fiscal.
- [ ] El manual enlaza a OpenAPI y a la colección Postman.
- [ ] El manual tiene fecha de última actualización y versión del contrato.
- [ ] La página es responsive y navegable con teclado.
- [ ] Los ejemplos no contienen API keys reales.
- [ ] El contenido se mantiene sincronizado con `docs/manual-integracion.md`.

## Escenarios Gherkin

```gherkin
Scenario: Acceso público al manual
  Given un visitante sin sesión
  When abre "/manual"
  Then ve el proceso de alta y la guía de API key
  And no se le solicita iniciar sesión
```

```gherkin
Scenario: Manual enlazado desde la home
  Given un visitante en la página principal
  When busca ayuda para integrar la API
  Then encuentra un enlace visible al manual
```

```gherkin
Scenario: Ejemplo de autenticación
  Given un desarrollador lee la sección de autenticación
  Then encuentra el header "X-Api-Key"
  And un ejemplo con una key de marcador de posición
  And una advertencia de no exponer la key en el frontend
```

```gherkin
Scenario: Códigos de error
  Given un desarrollador revisa la sección de errores
  Then encuentra los códigos 400, 401, 403, 404, 409, 429 y 500
  And la acción recomendada para cada uno
```

## Tareas técnicas

- [ ] Crear la página `/manual` en Razor Pages o Blazor.
- [ ] Escribir el contenido desde `docs/manual-integracion.md`.
- [ ] Agregar navegación desde la home, login y sección API keys.
- [ ] Agregar tabla de contenidos y anclas.
- [ ] Incluir bloques de código con botón de copiar.
- [ ] Enlazar OpenAPI y Postman.
- [ ] Revisar accesibilidad y responsive.
- [ ] Agregar un test de que la página responde `200`.
- [ ] Verificar que los ejemplos coincidan con los contratos implementados.
- [ ] Actualizar el manual cuando cambie un endpoint.

## Definition of Done

- [ ] Un tercero puede integrar la API siguiendo solo el manual.
- [ ] El manual funciona sin sesión y está enlazado desde el portal.
- [ ] Los ejemplos son copiables y no contienen secretos.
- [ ] La página pasa una revisión básica de accesibilidad.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el README.

## Contrato técnico

### Modelo de datos

US-30 no agrega colecciones, entidades ni endpoints de negocio. El contenido del manual vive en `docs/manual-integracion.md`, un archivo Markdown del repositorio que ya existe y que es la **única fuente de verdad** del contenido.

El portal solo lee ese archivo. No hay copia del texto en el código: si el manual se edita, la página cambia en el próximo build.

### Operaciones

| Operación | Ruta / método | Autenticación | Request | Response | Errores |
| --- | --- | --- | --- | --- | --- |
| Manual online | `GET /manual` | **Ninguna**. Página pública | — | HTML con el contenido renderizado del manual, tabla de contenidos y bloques de código con botón de copiar | `404` si el archivo fuente no está disponible en el build |
| Colección de Postman | `GET /downloads/FacturAR.postman_collection.json` | Ninguna | — | Archivo JSON de la colección | `404` |
| OpenAPI | `GET {Api:BaseUrl}/openapi/v1.json` | Ninguna | — | Documento OpenAPI de la API | `404` fuera de Development |

La página `/manual` es una **Razor Page** (`Pages/Manual.cshtml` + `Manual.cshtml.cs`) porque el contenido es estático y no necesita estado de componentes. El portal ya usa Razor Pages para `Pages/Verificar.cshtml` y componentes Blazor para las pantallas interactivas.

La ruta `/manual` no lleva el prefijo `/v1` porque no es parte de la API de negocio: es una página del portal, igual que `/` y `/verificar`.

### Generación del contenido

El contenido se **genera desde** `docs/manual-integracion.md` en tiempo de build. El flujo:

1. **Copia en build.** Un `Content` item del `.csproj` del portal enlaza `docs/manual-integracion.md` con `CopyToOutputDirectory="PreserveNewest"` y `Link="Content\manual-integracion.md"`. El archivo viaja al output del portal y queda disponible en runtime.
2. **Render.** La Razor Page lee el Markdown y lo convierte a HTML al construir la respuesta.
3. **Tabla de contenidos.** Se genera a partir de los encabezados `##` y `###` del Markdown. Cada encabezado recibe un `id` estable y la tabla de contenidos lista esos `id` como enlaces internos.
4. **Bloques de código.** Cada bloque cercado se renderiza como `<pre><code class="language-{lenguaje}">` y se le agrega un botón de copiar.
5. **Enlaces a OpenAPI y Postman.** La página agrega, fuera del Markdown, dos enlaces contextuales: al spec de la API (`{Api:BaseUrl}/openapi/v1.json`) y a la colección de Postman servida por el portal.

**Markdown → HTML.** Se usa `Markdig` (BSD-3-Clause). Es un **paquete NuGet nuevo** y su necesidad se justifica así: sin un renderer, el texto del manual habría que duplicar en HTML dentro del portal, y cualquier corrección quedaría desactualizada en uno de los dos lugares, que es exactamente lo que el criterio "El contenido se mantiene sincronizado con `docs/manual-integracion.md`" busca evitar. Alternativas descartadas:

- Escribir un renderer Markdown propio: es sobreingeniería y una fuente de XSS.
- Mantener el contenido en el `.razor` y agregar un test de sincronización: sigue duplicando el contenido y agrega trabajo manual.

Markdig se agrega **solo** al proyecto `FacturAR.Web`. Se usa con la extensión de identificadores automáticos para los anclas, con el HTML embebido deshabilitado (`DisableHtml`) para que ninguna etiqueta cruda del Markdown llegue al navegador, y sin resaltado de sintaxis para no sumar otra dependencia.

**Sincronización.** El build es la garantía principal. Como el Markdown se copia al output, un cambio en `docs/manual-integracion.md` se refleja sin tocar el portal. Un test de integración verifica que la página `/manual` contiene un ancla por cada `##` del Markdown: si alguien edita el `.md` y no el build, el test falla.

### Casos de uso y servicios

| Pieza | Capa | Responsabilidad |
| --- | --- | --- |
| `ManualRenderer` | `FacturAR.Web/Services` | Lee el Markdown, lo convierte a HTML con Markdig, extrae los encabezados para la tabla de contenidos y envuelve los bloques de código con su botón de copiar |
| `ManualPageModel` (`Pages/Manual.cshtml.cs`) | `FacturAR.Web/Pages` | Orquesta la carga del archivo y entrega a la vista el HTML, la tabla de contenidos y los metadatos (fecha de actualización, versión del contrato) |
| `wwwroot/js/manual.js` | `FacturAR.Web` | Comportamiento del botón de copiar con `navigator.clipboard.writeText` y realimentación accesible |
| `Api:BaseUrl` | `facturAR_frontend/src/FacturAR.Web/appsettings.json` | Base de la API para armar el enlace a `/openapi/v1.json`. Ya existe y se reutiliza |

El portal **no** accede a MongoDB: el manual se arma con archivos locales y configuración.

### Configuración

| Clave | Tipo | Default | Uso |
| --- | --- | --- | --- |
| `Manual__RutaArchivo` | `string` | `Content/manual-integracion.md` | Ubicación del Markdown dentro del output del portal |
| `Manual__VersionContrato` | `string` | `0.1.0` | Versión del contrato que se muestra en la página |
| `Manual__FechaActualizacion` | `string` | Fecha de build | Fecha de última actualización visible en la página |

`Api:BaseUrl` ya está configurado (`http://localhost:5171` en local) y se reutiliza para el enlace a OpenAPI.

El enlace a `/openapi/v1.json` solo resuelve en Development, porque el spec se publica únicamente en ese ambiente. En otros ambientes la página lo muestra con una nota que aclara que el spec se expone desde el repositorio versionado, para no dejar un enlace roto.

### UI

La página `/manual` tiene tres zonas:

| Zona | Contenido | Comportamiento |
| --- | --- | --- |
| Índice lateral | Tabla de contenidos generada desde los `##` y `###` | Sticky en escritorio. En pantallas chicas se pliega en un `<details>` |
| Contenido | HTML renderizado del Markdown | Una columna de lectura, ancho máximo cómodo |
| Metadatos | Fecha de última actualización y versión del contrato | Visible arriba del contenido |

Requisitos de accesibilidad y responsive:

- La página es navegable **solo con teclado**: el índice son enlaces `<a>` reales a los anclas, el botón de copiar es un `<button>` real con `aria-label` y foco visible.
- Hay un enlace de salto ("Ir al contenido") como primer elemento enfocable.
- El botón de copiar da realimentación accesible: cambia a "Copiado" y vuelve al estado anterior; el cambio se anuncia con `aria-live="polite"`.
- El contenido tiene `id` estables y únicos, derivados del texto del encabezado.
- La tabla de contenidos respeta el orden del documento, de modo que el recorrido con teclado coincide con el orden visual.
- La tipografía del contenido usa la escala del portal, no estilos inline.

Los ejemplos del manual **nunca** contienen API keys reales: se usan marcadores como `fak_7f3a9c2e_<secreto>`.

### Enlaces de navegación

La página debe ser alcanzable desde:

| Origen | Enlace |
| --- | --- |
| Home (`Components/Pages/Home.razor`) | Visible en la portada, sin sesión |
| Login (US-04) | Enlace en la pantalla de login |
| Sección de API keys (US-14) | Enlace "Guía de integración" |
| `docs/manual-integracion.md` | Enlace de vuelta al archivo fuente en el repositorio |

La navegación desde la home y el login se implementa en US-30; los enlaces dentro de las pantallas de login y de API keys se agregan con US-04 y US-14, o inmediatamente después, según el orden de implementación.

### Integración con otras historias

| Historia | Relación |
| --- | --- |
| US-02 | Aporta el contenido real de API keys: crear, listar, revocar y regenerar |
| US-03 | Aporta el contenido real de autenticación por `X-Api-Key` y su formato |
| US-05 | Aporta el contenido real de emisión, importes y CAE simulado |
| US-04 | Aporta el enlace desde la pantalla de login |
| US-14 | Aporta el enlace desde la sección de API keys |
| US-21 | Aporta la sincronización entre el Markdown y la documentación; el test de sincronización de US-21 verifica esta página |
| US-11 | Aporta el contenido de la verificación pública por CAE |
| US-19 | El portal se sirve en Docker y en CI; el Markdown debe viajar en la imagen |

### Documentación a sincronizar

- `docs/manual-integracion.md`: es la fuente. Debe incluir la fecha de última actualización y la versión del contrato que la página muestra.
- `README.md`: enlace al manual online y al archivo fuente.
- `docs/contratos-api.md`: los ejemplos del manual no pueden contradecir el contrato; los códigos de error del manual deben ser los canónicos de §2.1.

## Casos de prueba

| ID | Precondición | Pasos | Resultado esperado |
| --- | --- | --- | --- |
| CP-30.01 | Portal levantado | Solicitar `GET /manual` sin sesión | `200` con el HTML del manual; no se pide iniciar sesión ni se redirige al login |
| CP-30.02 | `GET /manual` | Revisar el índice lateral | La tabla de contenidos lista los encabezados `##` y `###` del manual, en orden, y cada entrada es un enlace a un ancla existente |
| CP-30.03 | `docs/manual-integracion.md` con 12 encabezados `##` | Contar los anclas `id` del HTML generado | Hay un ancla por encabezado y ninguno duplicado |
| CP-30.04 | `GET /manual` | Revisar los bloques de código | Cada bloque `<pre><code>` tiene un botón de copiar asociado y el texto del bloque es el mismo que el del Markdown |
| CP-30.05 | Página abierta en un navegador | Presionar el botón de copiar de un bloque | El portapapeles contiene el texto del bloque, el botón muestra "Copiado" y el cambio se anuncia por `aria-live` |
| CP-30.06 | `GET /manual` | Buscar los enlaces externos | Hay un enlace a `{Api:BaseUrl}/openapi/v1.json` y otro a la colección de Postman (`/downloads/FacturAR.postman_collection.json`) |
| CP-30.07 | Portal en Development | Seguir el enlace de OpenAPI | Devuelve el documento OpenAPI de la API |
| CP-30.08 | Portal fuera de Development | Seguir el enlace de OpenAPI | La página aclara que el spec solo se expone en Development y ofrece el enlace al spec versionado en el repositorio |
| CP-30.09 | `GET /manual` | Revisar la cabecera | Se muestran la fecha de última actualización y la versión del contrato, y coinciden con las del Markdown |
| CP-30.10 | `GET /manual` | Buscar el patrón de una API key | Todos los ejemplos usan marcadores de posición; no hay ninguna key real en el contenido |
| CP-30.11 | Página abierta | Recorrer la página solo con teclado | El enlace de salto enfoca el contenido, el índice es navegable con `Tab` y el botón de copiar se puede activar con `Enter` y `Espacio`; el foco siempre es visible |
| CP-30.12 | Ventana de 375 px de ancho | Abrir `/manual` | El índice se pliega en un control expandible y el contenido ocupa el ancho completo sin scroll horizontal |
| CP-30.13 | `docs/manual-integracion.md` modificado sin regenerar | Ejecutar el test de sincronización | El test falla e informa qué encabezado quedó desactualizado |
| CP-30.14 | `docs/manual-integracion.md` con HTML crudo | Regenerar la página | El HTML crudo no aparece en la respuesta: se muestra escapado o se omite |
| CP-30.15 | Home del portal | Buscar el enlace al manual | Hay un enlace visible al manual en la portada |

## Dependencias técnicas

| Dependencia | Historia que la introduce | Detalle técnico requerido |
| --- | --- | --- |
| Contenido de API keys | US-02 | Sección "Crear una API key" con el flujo real y el formato `fak_<prefix>_<secreto>` |
| Autenticación por API key | US-03 | Sección "Autenticar requests" con el header `X-Api-Key` y su comportamiento ante `API_KEY_AUSENTE` y `API_KEY_INVALIDA` |
| Emisión de comprobantes | US-05 | Sección "Emitir una factura" con el request real, la respuesta real y las reglas de importes |
| Verificación pública | US-11 | Sección "Verificar un comprobante" con el endpoint público |
| Pantalla de login | US-04 | Enlace al manual desde el login. La página del manual es pública y no depende de la sesión |
| Sección de API keys del portal | US-14 | Enlace "Guía de integración" en la pantalla de API keys |
| Documentación canónica | Base existente | `docs/contratos-api.md` §2.1 como lista única de códigos de error, y `docs/manual-integracion.md` como fuente del contenido |
| Sincronización de la documentación | US-21 | El test que verifica que la página y el Markdown están alineados |
| Build y publicación del portal | US-19, US-20 | El Markdown debe quedar en la imagen del portal; `docs/` se copia en la etapa de build |
| Paquete NuGet nuevo | Esta historia | `Markdig` (BSD-3-Clause) solo en `FacturAR.Web`, versión fijada y justificada en el PR. No se agrega ningún paquete a los proyectos del backend |

## Notas de alcance

- No implementa buscador ni comentarios.
- No implementa documentación interactiva generada desde OpenAPI.
- No reemplaza OpenAPI; lo complementa con una guía de uso.
- No incluye ejemplos de AFIP real.
