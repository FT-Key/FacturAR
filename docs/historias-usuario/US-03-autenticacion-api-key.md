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

- [ ] Los endpoints de negocio exigen el header `X-Api-Key`.
- [ ] Una key ausente devuelve `401 API_KEY_REQUERIDA`.
- [ ] Una key mal formada, inexistente, revocada o de una empresa inactiva devuelve `401 API_KEY_INVALIDA`.
- [ ] La comparación del hash se hace en tiempo constante.
- [ ] Una key válida resuelve el `ContribuyenteId` y lo adjunta al request.
- [ ] El contexto de tenant se expone mediante una abstracción de Application.
- [ ] El cliente no puede acceder a datos de otro contribuyente.
- [ ] Se actualiza `UltimoUso` en cada autenticación exitosa.
- [ ] La actualización de `UltimoUso` no bloquea la respuesta si falla.
- [ ] La key completa no se registra en logs, errores ni trazas.
- [ ] Los endpoints de emisión y consulta quedan protegidos.
- [ ] La verificación pública permanece anónima.
- [ ] Los errores no revelan si el prefijo existe.
- [ ] Hay tests de 401, 403, aislamiento y actualización de último uso.

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
  Then la API devuelve 401 con código "API_KEY_REQUERIDA"
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

- [ ] Crear `IApiKeyAuthenticator` y `ITenantContext` en Application.
- [ ] Implementar `ApiKeyAuthenticationHandler` o middleware en Api.
- [ ] Parsear el prefijo y buscar la key en el repositorio.
- [ ] Comparar el hash con `CryptographicOperations.FixedTimeEquals`.
- [ ] Rechazar keys de contribuyentes inactivos.
- [ ] Actualizar `UltimoUso` de forma controlada.
- [ ] Aplicar el esquema de autenticación a los endpoints de negocio.
- [ ] Mantener `/v1/comprobantes/verificar/{cae}` anónimo.
- [ ] Agregar tests de autenticación y aislamiento.
- [ ] Documentar el header, errores y ejemplos en el manual.

## Definition of Done

- [ ] Los endpoints protegidos rechazan requests sin key.
- [ ] Una key válida solo accede a su empresa.
- [ ] La verificación pública sigue funcionando sin autenticación.
- [ ] Los logs no contienen keys completas.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el manual.

## Notas de alcance

- No implementa OAuth, JWT ni firma HMAC.
- No implementa scopes por endpoint.
- No implementa rotación automática de keys.
- No implementa bloqueo por IP.
