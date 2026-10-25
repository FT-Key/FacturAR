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

## Notas de alcance

- No implementa buscador ni comentarios.
- No implementa documentación interactiva generada desde OpenAPI.
- No reemplaza OpenAPI; lo complementa con una guía de uso.
- No incluye ejemplos de AFIP real.
