# US-21 — Tests, Postman y documentación

**Épica:** Calidad  
**Prioridad:** Alta  
**Estimación:** 2 días  
**Requisito:** RNF-13, RNF-17, RNF-18  
**Depende de:** todas las historias implementadas; CI depende de US-20 y CD de US-31  

## Historia

Como equipo, quiero tests y documentación actualizada para demostrar el sistema, facilitar el mantenimiento y permitir que un tercero lo levante y lo pruebe sin ayuda.

## Contexto

La suite debe cubrir reglas de negocio, endpoints, Mongo, onboarding, emisión y verificación. Postman, OpenAPI, README y el manual deben coincidir con el código.

## Criterios de aceptación

- [ ] Los tests unitarios cubren CUIT, CAE, contraseñas, tokens, numeración, idempotencia y reglas de emisión.
- [x] Hay al menos un test de integración de la API con MongoDB.
- [ ] Hay tests de solicitud, aprobación, invitación, login, API keys y aislamiento.
- [ ] Hay tests de emisión, notas, consultas y verificación.
- [x] Existe una colección de Postman o un archivo `.http` con los endpoints principales.
- [x] El README explica cómo levantar el sistema local y con Docker.
- [x] OpenAPI se genera y está disponible en Development.
- [x] Las historias y requisitos están enlazados desde el README.
- [ ] El manual de integración está publicado en el portal.
- [ ] `dotnet test FacturAR.slnx` pasa en CI.
- [ ] La documentación no contiene secretos ni datos reales.
- [ ] Los códigos de error están documentados y coinciden con la API.

## Escenarios Gherkin

```gherkin
Scenario: Suite local
  Given el repositorio clonado y las dependencias restauradas
  When se ejecuta "dotnet test FacturAR.slnx"
  Then todos los tests pasan o se omiten los MongoFact sin connection string
```

```gherkin
Scenario: Documentación de onboarding
  Given un tercero lee el README y el proceso de alta
  When sigue los pasos
  Then puede solicitar acceso, aprobar, activar, iniciar sesión y crear una API key
```

```gherkin
Scenario: Postman
  Given la colección importada
  When ejecuta la verificación pública
  Then obtiene la respuesta esperada
```

```gherkin
Scenario: CI
  Given un pull request válido
  When se ejecuta el workflow
  Then build y tests pasan
```

## Tareas técnicas

- [x] Crear tests unitarios de CUIT, CAE y verificación.
- [x] Crear tests de integración de la API en memoria, sin MongoDB.
- [x] Crear el archivo `FacturAR.Api.http`.
- [x] Escribir README, requisitos, modelos e historias.
- [x] Crear una colección de Postman.
- [x] Agregar un test de integración con base de datos.
- [ ] Agregar tests de onboarding y emisión.
- [ ] Publicar el manual en el portal.
- [ ] Actualizar Postman con los endpoints nuevos.
- [ ] Ejecutar la suite en CI.
- [ ] Revisar que la documentación coincida con el código.

## Definition of Done

- [ ] Un tercero puede levantar y probar el sistema siguiendo el README.
- [ ] La suite de tests pasa en local y en CI.
- [ ] Postman, OpenAPI y manual están sincronizados.
- [ ] No hay secretos en la documentación.
- [ ] La historia está enlazada desde el índice y el README.

## Evidencia

- `docs/postman/FacturAR.postman_collection.json` y environment local.
- `MongoApiTests` se ejecuta contra MongoDB real y se omite sin connection string.
- `ApiEndpointsTests` verifica OpenAPI en Development.
- Suite local: 18 tests unitarios y 9 de integración aprobados.
- Pendiente: tests de onboarding/emisión y ejecución en CI, que depende de US-20; el deploy en Azure depende de US-31.

## Notas de alcance

- No implementa cobertura de código obligatoria.
- No implementa tests de carga.
- No implementa pruebas de penetración.
