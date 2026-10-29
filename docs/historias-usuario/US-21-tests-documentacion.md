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

## Contrato técnico

US-21 no agrega endpoints, colecciones ni entidades. Su alcance es de calidad y documentación: la suite de tests, los artefactos de Postman y la sincronización de la documentación con el código real.

### Modelo de datos

No hay cambios de modelo. Los tests de integración con MongoDB crean una base temporal y la eliminan al finalizar:

| Recurso | Valor |
| --- | --- |
| Prefijo de base temporal | `facturar_it_` + `Guid.NewGuid():N`, recortado a 28 caracteres (`facturar_it_*`, máximo 38 bytes) |
| Origen de la connection string | `MONGO_CONNECTION_STRING` o `Mongo__ConnectionString` |
| Omisión | Sin connection string, los tests marcados `[MongoFact]` se omiten con un mensaje explícito |
| Limpieza | `DropDatabaseAsync` en `DisposeAsync` |

### Matriz mínima de tests por historia

El nivel de test sigue la regla de `AGENTS.md`: unitarios para reglas, integración para endpoints, Mongo y configuración.

| Historia | Unitarios | Integración con `WebApplicationFactory` | Integración con Mongo (`MongoFact`) |
| --- | --- | --- | --- |
| US-01 | Normalización y validación de CUIT, estado de solicitud | `POST /v1/solicitudes-alta`: `201`, `VALIDACION`, `SOLICITUD_PENDIENTE` | Solicitud persistida sin contribuyente ni usuario |
| US-02 | Formato de la key, hash y comparación en tiempo constante | `GET/POST /v1/api-keys`, `DELETE`, `POST /v1/api-keys/{id}/regenerar` | Key persistida con `Hash` y sin el valor completo |
| US-03 | Resolución de key a contribuyente | `401 API_KEY_AUSENTE` y `401 API_KEY_INVALIDA` | Aislamiento: una key no habilita otra empresa |
| US-04 | Política de contraseñas, sesión y roles | Login, logout, `me`, `SIN_PERMISOS`, `CUENTA_INACTIVA` | Cookie de sesión y aislamiento multiempresa |
| US-05 | Cálculo de importes, IVA por tipo, alícuotas, CAE | `POST /v1/comprobantes`: `201`, `EMISION_INVALIDA`, `VALIDACION` | Comprobante persistido con `Decimal128` y CAE único |
| US-06 | Hash del payload canónico | `Idempotency-Key` repetida con el mismo y con otro payload | Índice único `(ContribuyenteId, IdempotencyKey)` y `IDEMPOTENCIA_CONFLICTO` |
| US-07 | Formato del número | Numeración correlativa por punto de venta y tipo | `findOneAndUpdate` atómico y secuencia implícita |
| US-08 | Saldo ajustable y compatibilidad de tipo | `400 TIPO_NOTA_INCOMPATIBLE`, `400 IMPORTE_EXCEDIDO` | Nota persistida con su propio CAE y secuencia |
| US-09 | Cálculo del último autorizado | `GET /v1/comprobantes`, `GET /v1/comprobantes/{id}`, `ultimo-autorizado` | Paginación y orden estable |
| US-10 | Generación del QR | `GET /v1/comprobantes/{id}/pdf` devuelve `application/pdf` | QR embebido y contenido del PDF |
| US-11 | Validación de CAE | `GET /v1/comprobantes/verificar/{cae}`: `400 CAE_INVALIDO`, `404 COMPROBANTE_NO_ENCONTRADO` | Ausencia de campos internos en la respuesta |
| US-12 | Filtros de listado | Listado con filtros y paginación | Orden `FechaEmision` descendente estable |
| US-13 | Validación de la emisión manual | Emisión desde el portal | Comprobante emitido por el portal |
| US-14 | Formato de la key en la UI | Listado, revocación y regeneración desde el portal | Key revocada deja de autenticar |
| US-15 | Mapeo XML ↔ DTO | `GET /soap?wsdl`, `FECAESolicitar`, `FECAEGenerar`, `Fault` | Comprobante emitido por SOAP con el mismo numbering que REST |
| US-16 | Mapeo XML ↔ DTO de consulta | `FECompUltimoAutorizado`, `FECompConsultar`, `FEDummy` | Aislamiento: un comprobante ajeno responde `404` |
| US-17 | Reglas de enmascaramiento y ciclo de estados | — | Evento de auditoría y worker de la outbox con reintentos |
| US-18 | Orden de resolución de parámetros | Seed idempotente vía `WebApplicationFactory` | Conteo de documentos antes y después del seed |
| US-27 | Transiciones de estado de la solicitud | `GET /v1/admin/solicitudes`, aprobar, rechazar | Aprobación transaccional: contribuyente, punto de venta, invitación y solicitud |
| US-28 | Estados de la invitación | `GET /v1/invitaciones/{token}`, `POST .../aceptar` | Token hasheado, un solo uso, `INVITACION_VENCIDA` |
| US-29 | Plantillas y enmascaramiento | `502 EMAIL_NO_ENVIADO` sin revertir la operación de negocio | `emails` persistido sin el token completo |

### Andamiaje de tests

| Pieza | Ubicación | Rol |
| --- | --- | --- |
| `CustomWebApplicationFactory` | `facturAR_backend/tests/FacturAR.IntegrationTests/` | Levanta la API en memoria con `Mongo:InitializeIndexes=false` |
| `MongoFactAttribute` | `facturAR_backend/tests/FacturAR.IntegrationTests/` | Omite el test si no hay connection string |
| `MongoTestSettings` | `facturAR_backend/tests/FacturAR.IntegrationTests/` | Lee la connection string y agrega `serverSelectionTimeoutMS=5000` |
| `MongoApiTests` | `facturAR_backend/tests/FacturAR.IntegrationTests/` | Patrón de base temporal: nombre corto, se elimina al final |
| `ApiEndpointsTests` | `facturAR_backend/tests/FacturAR.IntegrationTests/` | Verifica OpenAPI y los endpoints públicos en Development |

Convenciones que debe cumplir la suite:

- Todo comportamiento nuevo tiene test en el nivel adecuado. No se desactiva ni se borra un test para que la suite pase.
- Un test de integración no depende de servicios externos salvo los marcados `[MongoFact]`.
- Los tests no dejan datos: cada base temporal se elimina al finalizar.
- Los tests no verifican datos sensibles: nunca se valida que un log contenga una key, un token o una contraseña.

### Artefactos de Postman y HTTP

| Artefacto | Ubicación | Formato |
| --- | --- | --- |
| Colección | `docs/postman/FacturAR.postman_collection.json` | Postman Collection v2.1.0 |
| Environment | `docs/postman/FacturAR.local.postman_environment.json` | Postman environment, nombre `FacturAR local` |
| Archivo `.http` | `facturAR_backend/src/FacturAR.Api/FacturAR.Api.http` | Requests HTTP con `@FacturAR_Api_HostAddress` |

Formato de la colección:

- Carpetas por dominio: `Sistema`, `Comprobantes`, `API keys`, `Onboarding`, `SOAP`.
- Cada request lleva un script `event.test` con al menos una aserción de código de estado y una del cuerpo.
- Las variables viven en el environment (`baseUrl`, `apiKey`, `idempotencyKey`, `contribuyenteId`, `caeExistente`), nunca hardcodeadas en la URL del request.
- El environment **no** contiene API keys reales: `apiKey` usa el placeholder `fak_<12 hex>_<43 base64url>`.
- La colección documenta los endpoints **implementados**. Los endpoints de historias pendientes van en una carpeta aparte, `Pendiente (no implementado)`, con el `US-XX` en la descripción, para que nadie los ejecute esperando una respuesta.
- `baseUrl` del environment apunta a `http://localhost:8080` (Docker). Para ejecución local hay que cambiarlo a `http://localhost:5171`, como ya indica el README.

El `.http` de la API es el resumen ejecutable desde el IDE: mantiene los mismos paths que la colección y sirve como smoke test manual.

### OpenAPI

- En Development, `GET /openapi/v1.json` describe los endpoints de `FacturAR.Api`.
- Las rutas de `FacturAR.Web` (portal) no aparecen en el OpenAPI de la API: son otra aplicación. La página `/manual` del portal (US-30) enlaza al spec de la API.
- El OpenAPI no incluye el endpoint SOAP: no es un endpoint REST. El WSDL (`/soap?wsdl`) es su equivalente y se documenta aparte.

### Documentación: regla de veracidad

Este es el punto crítico de la historia. `docs/manual-integracion.md` y `docs/proceso-alta-empresa.md` describen hoy login, invitaciones, API keys, emisión, PDF y verificación, que **no existen todavía** en el código. US-21 debe evitar que un tercero los tome por implementados.

Reglas obligatorias:

1. **Nada se documenta como existente si no existe.** Cada endpoint, flujo o sección que dependa de una historia no completada lleva un bloque visible de estado, con la historia que lo habilita:

   ```markdown
   > **Diseño objetivo — US-05.** Este endpoint todavía no está implementado.
   > Responde `404` hasta que US-05 se complete.
   ```

2. **No se inventan endpoints.** Un endpoint solo puede aparecer en la documentación si existe en el código o si pertenece a una historia aprobada, y en ese caso con su marca de "diseño objetivo". No se agregan rutas, headers, campos ni códigos que no estén en `docs/contratos-api.md` o en el código.

3. **Los códigos de error son los canónicos.** La tabla de `docs/manual-integracion.md` §10 usa hoy `API_KEY_REQUERIDA`, `NO_AUTORIZADO`, `SIN_PERMISO`, `NO_ENCONTRADO` y `CONFLICTO`, y un campo `detalles` en el cuerpo del error que no existe en `ErrorResponse`. US-21 debe corregirlo:

   - Reemplazar cada código por su equivalente canónico de `docs/contratos-api.md` §2.1: `API_KEY_AUSENTE`, `API_KEY_INVALIDA`, `CREDENCIALES_INVALIDAS`, `SIN_PERMISOS`, `COMPROBANTE_NO_ENCONTRADO`, `IDEMPOTENCIA_CONFLICTO`, entre otros.
   - Eliminar los códigos que no existen en la lista canónica, entre ellos `CUIT_YA_REGISTRADO` del README.
   - Corregir el cuerpo del error a `ErrorResponse(codigo, mensaje)`, sin `detalles`. Los errores de validación no enumeran campos salvo que la historia lo pida.

4. **El README también se corrige.** La sección "Códigos de error" debe usar la lista canónica y marcar como planificados los endpoints que aún no existen, como ya hace con la tabla de endpoints planificados.

5. **Coherencia con el manual online.** El contenido de `docs/manual-integracion.md` es la fuente de la página `/manual` (US-30). Si el manual cambia, la página se regenera; si no, la página queda desactualizada. US-21 es la historia que verifica esa coherencia.

6. **Sin secretos.** Ni el manual, ni el README, ni la colección de Postman, ni el `.http` contienen API keys, tokens, contraseñas ni connection strings reales. Los ejemplos usan marcadores de posición.

### Comandos de verificación

```powershell
dotnet build FacturAR.slnx -c Release
dotnet test FacturAR.slnx -c Release --no-build --logger "console;verbosity=minimal"
```

- La suite local debe pasar completa o bien omitir los `[MongoFact]` sin connection string.
- En CI (`.github/workflows/ci.yml`, US-20) la suite corre con el mismo comando y el job es bloqueante.
- El CD (US-31) no ejecuta tests: solo se apoya en el build ya validado.

### Configuración

US-21 no agrega claves de configuración. La única clave que aparece en los artefactos es la variable de entorno `MONGO_CONNECTION_STRING` (o `Mongo__ConnectionString`) que habilita los tests `[MongoFact]`, y la que ya usa el proyecto. Ninguna documentación puede incluir connection strings reales: se documentan los **nombres** de las variables, nunca sus valores.

### Integración con otras historias

| Historia | Relación |
| --- | --- |
| Todas las historias implementadas | Aportan los casos de uso y los endpoints que hay que testear y documentar |
| US-20 | Aporta el workflow de CI que corre la suite y bloquea el merge |
| US-31 | Aporta el CD; US-21 no lo modifica |
| US-30 | Publica el manual online. US-21 garantiza que el contenido del manual sea fiel al código |
| US-19 | Aporta Docker Compose y el `.env` para levantar el sistema localmente |

## Casos de prueba

| ID | Precondición | Pasos | Resultado esperado |
| --- | --- | --- | --- |
| CP-21.01 | Repositorio clonado y dependencias restauradas | Ejecutar `dotnet test FacturAR.slnx` | Todos los tests pasan; los `[MongoFact]` se omiten con mensaje cuando no hay connection string |
| CP-21.02 | `MONGO_CONNECTION_STRING` configurada | Ejecutar los tests `[MongoFact]` | Corren contra MongoDB real, validan el comportamiento y eliminan la base `facturar_it_*` al terminar |
| CP-21.03 | Colección importada en Postman con el environment local | Ejecutar el request de verificación pública | Devuelve la respuesta esperada y los `pm.test` del request pasan |
| CP-21.04 | Environment local abierto | Revisar los valores | `apiKey` es un placeholder con el formato `fak_<12 hex>_<43 base64url>`; no hay ninguna key real |
| CP-21.05 | `FacturAR.Api.http` abierto | Revisar los requests | Contiene los endpoints públicos implementados y usa `@FacturAR_Api_HostAddress` |
| CP-21.06 | API en Development | Solicitar `GET /openapi/v1.json` | El documento se genera y contiene las rutas `/v1` implementadas |
| CP-21.07 | `docs/manual-integracion.md` leído | Comparar su tabla de errores con `docs/contratos-api.md` §2.1 | Todos los códigos documentados son canónicos; no quedan `API_KEY_REQUERIDA`, `NO_AUTORIZADO`, `SIN_PERMISO`, `NO_ENCONTRADO` ni `CONFLICTO` |
| CP-21.08 | `docs/manual-integracion.md` leído | Revisar la sección de emisión, de PDF y de API keys | Cada una lleva el bloque "Diseño objetivo" con su `US-XX` y aclara que el endpoint todavía no existe |
| CP-21.09 | `docs/proceso-alta-empresa.md` leído | Revisar la sección "Endpoints previstos" | Cada ruta prevista lleva su `US-XX` y está marcada como no implementada |
| CP-21.10 | README leído | Revisar la sección "Códigos de error" y la de endpoints | Usa los códigos canónicos, no incluye `CUIT_YA_REGISTRADO` y separa lo implementado de lo planificado |
| CP-21.11 | Repositorio completo | Buscar secretos (`fak_` seguidos de un secreto real, connection strings, contraseñas) | No hay secretos en el código, la documentación, la colección ni el `.http` |
| CP-21.12 | Pull request válido | Ejecutar el workflow de CI | Build y tests pasan y el check bloquea el merge |
| CP-21.13 | README y `.env.example` | Ejecutar el seed siguiendo el README | El README describe las claves correctas (`Seed__Enabled`, `Seed__Password`) y el seed se puede ejecutar en un entorno limpio |
| CP-21.14 | `docs/manual-integracion.md` modificado | Regenerar la página `/manual` y correr la suite | La página refleja el cambio; si el contenido se duplicó o quedó desactualizado, el test de sincronización falla |

## Dependencias técnicas

| Dependencia | Historia que la introduce | Detalle técnico requerido |
| --- | --- | --- |
| Casos de uso implementados | Todas | Los endpoints y reglas a testear ya existen en `FacturAR.Api`, `FacturAR.Application` y `FacturAR.Infrastructure` |
| Andamiaje de tests | Base existente | `CustomWebApplicationFactory`, `MongoFactAttribute` y `MongoTestSettings` ya están disponibles y no se modifican |
| Contrato canónico | Base existente | `docs/contratos-api.md` §2.1 es la lista de códigos de error contra la que se audita la documentación |
| Colección y environment de Postman | Base existente | `docs/postman/FacturAR.postman_collection.json` y `docs/postman/FacturAR.local.postman_environment.json` ya existen y se amplían |
| CI | US-20 | `.github/workflows/ci.yml` corre la suite; US-21 agrega los casos faltantes, no el workflow |
| CD | US-31 | `.github/workflows/cd.yml` no ejecuta tests; US-21 no lo modifica |
| Manual online | US-30 | La página `/manual` consume `docs/manual-integracion.md`; el test de sincronización depende de que US-30 exista |
| Levantar el sistema local | US-19 | `docker compose --env-file .env up --build -d` y los `.env` de ejemplo |

## Notas de alcance

- No implementa cobertura de código obligatoria.
- No implementa tests de carga.
- No implementa pruebas de penetración.
