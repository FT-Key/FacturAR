# US-16 — SOAP de consultas

**Épica:** Interoperabilidad  
**Prioridad:** Media  
**Estimación:** 1 día  
**Requisito:** RF-16  
**Depende de:** US-03, US-09  
**Habilita:** US-21

## Historia

Como sistema cliente legado, quiero consultar el último comprobante autorizado, obtener un comprobante y verificar el estado del servicio por SOAP para integrarme sin usar REST.

## Contexto

El SOAP de consultas reutiliza los casos de uso de US-09 y el contexto de tenant. `FEDummy` es un health check del servicio simulado.

## Criterios de aceptación

- [ ] Se expone `FECompUltimoAutorizado` con punto de venta y tipo de comprobante.
- [ ] Se expone `FECompConsultar` por tipo, punto de venta y número.
- [ ] Se expone `FEDummy` con estado del servicio.
- [ ] Las consultas se filtran por el contribuyente autenticado.
- [ ] Un comprobante de otra empresa no se devuelve.
- [ ] El último autorizado devuelve `0` si no hay comprobantes.
- [ ] Los errores se devuelven como `Fault` o resultado controlado.
- [ ] El WSDL incluye las tres operaciones.
- [ ] Hay tests de consulta, aislamiento y `FEDummy`.
- [ ] El contrato está documentado con ejemplos.

## Escenarios Gherkin

```gherkin
Scenario: Último autorizado
  Given una empresa con última factura C número 42
  When invoca FECompUltimoAutorizado para factura C
  Then recibe 42
```

```gherkin
Scenario: Consultar comprobante
  Given un comprobante propio número 42
  When invoca FECompConsultar
  Then recibe los datos fiscales del comprobante
```

```gherkin
Scenario: Comprobante ajeno
  Given un comprobante de la empresa B
  When la empresa A lo consulta por SOAP
  Then recibe un error controlado sin datos
```

```gherkin
Scenario: FEDummy
  Given el servicio está operativo
  When invoca FEDummy
  Then recibe estado "OK"
```

## Tareas técnicas

- [ ] Definir las operaciones SOAP y el WSDL.
- [ ] Implementar `FECompUltimoAutorizado`.
- [ ] Implementar `FECompConsultar`.
- [ ] Implementar `FEDummy`.
- [ ] Resolver autenticación y tenant.
- [ ] Reutilizar los casos de uso de consulta.
- [ ] Mapear errores.
- [ ] Agregar tests de contrato y aislamiento.
- [ ] Documentar ejemplos.

## Definition of Done

- [ ] Las tres operaciones responden según contrato.
- [ ] Las consultas están aisladas por empresa.
- [ ] El WSDL se puede consumir.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el manual.

## Contrato técnico

### Modelo de datos

US-16 **no agrega colecciones ni índices**. Es de solo lectura:

| Colección | Consulta | Índice usado |
| --- | --- | --- |
| `comprobantes` | Comprobante por `(ContribuyenteId, PuntoVenta, TipoComprobante, Numero)` y último emitido por `(ContribuyenteId, PuntoVenta, TipoComprobante)` | `ux_comprobantes_numeracion`, `ix_comprobantes_contribuyente_fecha` |
| `secuencias` | Último número emitido, como respaldo del contador | `ux_secuencias_contribuyente_punto_tipo` |

El filtro por `ContribuyenteId` se aplica **siempre**, tomado de la API key autenticada. Un `Numero` de otra empresa se trata como inexistente: la respuesta es `COMPROBANTE_NO_ENCONTRADO` y no revela la existencia del comprobante ajeno.

### Operaciones SOAP

Las tres operaciones comparten el endpoint `/soap` y el WSDL `/soap?wsdl` con US-15. El WSDL publicado incluye `FECompUltimoAutorizado`, `FECompConsultar` y `FEDummy` además de las operaciones de US-15.

| Operación | Ruta / método | Autenticación | Request | Response | Errores |
| --- | --- | --- | --- | --- | --- |
| `FECompUltimoAutorizado` | `POST /soap` (`SOAPAction: .../FECompUltimoAutorizado`) | API key en `X-Api-Key` (o `Auth/Token`) | `PtoVta` (int), `TipoComprobante` (int) | `Resultado` (`"A"`/`"R"`), `UltimoNro` (long), `Cae`, `FechaEmision`, `FchVto`, `PtoVta`, `TipoComprobante` | `VALIDACION` 400, `API_KEY_AUSENTE` 401, `API_KEY_INVALIDA` 401, `MONGO_NO_DISPONIBLE` 503, `ERROR_INTERNO` 500 |
| `FECompConsultar` | `POST /soap` (`SOAPAction: .../FECompConsultar`) | API key en `X-Api-Key` (o `Auth/Token`) | `TipoComprobante` (int), `PtoVta` (int), `Numero` (long) | `Resultado` (`"A"`/`"R"`), `Cuit`, `PtoVta`, `TipoComprobante`, `Numero`, `FechaEmision`, `Cae`, `FchVto`, `DocTipo`, `DocNro`, `CondicionIVA`, `Moneda`, `ImporteNeto`, `ImporteIva`, `ImporteTotal`, `Observaciones` | `VALIDACION` 400, `COMPROBANTE_NO_ENCONTRADO` 404, `API_KEY_AUSENTE` 401, `API_KEY_INVALIDA` 401, `MONGO_NO_DISPONIBLE` 503, `ERROR_INTERNO` 500 |
| `FEDummy` | `POST /soap` (`SOAPAction: .../FEDummy`) | API key en `X-Api-Key` (o `Auth/Token`) | Sin cuerpo de negocio | `Estado` (`"OK"`), `ServerTime` (ISO 8601 UTC), `Version`, `Service` | `API_KEY_AUSENTE` 401, `API_KEY_INVALIDA` 401, `ERROR_INTERNO` 500 |

Reglas de operación:

- **Mismos DTOs que REST.** `FECompConsultar` devuelve exactamente los campos del DTO de lectura de US-09 (`ComprobanteResponse`): no agrega ni quita campos. La forma XML se deriva del DTO por mapeo explícito, no por serialización directa, para que REST y SOAP no puedan divergir. `FECompUltimoAutorizado` devuelve los mismos campos del DTO de "último autorizado" de US-09.
- **Último autorizado.** Devuelve el comprobante de mayor `Numero` en estado `Emitido` para `(ContribuyenteId, PuntoVenta, TipoComprobante)`. Si no hay ninguno, `UltimoNro` = `0` y los campos opcionales quedan vacíos. No es un error.
- **Aislamiento.** Un `Numero` de otra empresa produce `COMPROBANTE_NO_ENCONTRADO` 404 con el mismo cuerpo que un número inexistente. Nunca se devuelve un comprobante ajeno.
- **`FEDummy` es el chequeo del servicio SOAP**, no de MongoDB: no consulta la base y por eso no detecta una caída de Mongo. La disponibilidad de Mongo se verifica con `GET /health`. `FEDummy` **exige** API key: es coherente con que todas las consultas se filtren por el contribuyente autenticado y con que el SOAP no omita autenticación.
- **Errores de negocio vs. fallos de protocolo.** Igual que en US-15: un error de negocio viaja en el sobre `R` con HTTP `200`; un fallo de protocolo o de autenticación viaja como `SOAP Fault` con el código canónico en `faultstring` y en `detail`. El `Fault` de autenticación conserva su HTTP `401`.
- **Sin escritura.** Ninguna de las tres operaciones escribe en MongoDB.

### Casos de uso y servicios

| Pieza | Capa | Responsabilidad |
| --- | --- | --- |
| `IContractConsultas` | `FacturAR.Api/Soap/` | `[ServiceContract]` con los tres `[OperationContract]`; se registra en el mismo endpoint que `IContractFecae` de US-15 |
| DTOs XML de consulta | `FacturAR.Contracts/Soap/` | Contrato serializado con `XmlSerializer`, con los mismos campos que los DTOs REST |
| `ConsultasMapeador` | `FacturAR.Api/Soap/` | Traduce el request XML a los parámetros de los casos de uso y la respuesta al sobre AFIP |
| `VerificarComprobanteService` / `ObtenerComprobanteService` / `ObtenerUltimoAutorizadoService` | `FacturAR.Application` (US-09) | Casos de uso de consulta. **No se modifican** |
| `IApiKeyValidator` | `FacturAR.Application` (US-03) | Resuelve el tenant. Se reutiliza tal como en US-15 |

US-16 no implementa reglas de consulta propias: solo traduce entre XML y los casos de uso de US-09.

### Configuración

US-16 no agrega claves de configuración propias. Reusa `Soap__Habilitado` y `Soap__Ruta` de US-15, porque comparte endpoint y WSDL.

### Integración con otras historias

| Historia | Relación |
| --- | --- |
| US-15 | Aporta el endpoint, el WSDL, SoapCore y el andamiaje de autenticación. US-16 **extiende** el mismo contrato, no crea un servicio nuevo |
| US-03 | Aporta la autenticación por API key y la resolución del tenant |
| US-09 | Aporta los casos de uso de consulta y los DTOs que el SOAP debe replicar |
| US-05 | Aporta los comprobantes que se consultan |
| US-21 | Debe documentar las tres operaciones con ejemplos y agregarlas a la colección Postman |
| US-30 | El manual online debe incluir la sección de interoperabilidad SOAP |

### Documentación a sincronizar

- `docs/contratos-api.md`: agregar las tres operaciones de consulta al contrato SOAP.
- `docs/manual-integracion.md`: ejemplos de `FECompUltimoAutorizado`, `FECompConsultar` y `FEDummy`.
- `README.md`: el WSDL `/soap?wsdl` ya queda listado por US-15; esta historia no agrega rutas nuevas.

## Casos de prueba

| ID | Precondición | Pasos | Resultado esperado |
| --- | --- | --- | --- |
| CP-16.01 | API levantada con US-15 | Solicitar `GET /soap?wsdl` | `200` y el WSDL declara `FECompUltimoAutorizado`, `FECompConsultar` y `FEDummy` con sus `SOAPAction` |
| CP-16.02 | Empresa con última factura C `Numero` 42 | Invocar `FECompUltimoAutorizado` con `PtoVta` 1 y `TipoComprobante` 11 | `Resultado` = `"A"` y `UltimoNro` = `42` |
| CP-16.03 | Empresa sin comprobantes del tipo pedido | Invocar `FECompUltimoAutorizado` | `Resultado` = `"A"` y `UltimoNro` = `0`, sin error |
| CP-16.04 | Comprobante propio `Numero` 42 | Invocar `FECompConsultar` con tipo, `PtoVta` y `Numero` | `Resultado` = `"A"` y los datos fiscales coinciden con el comprobante almacenado |
| CP-16.05 | Comprobante de la empresa B | Invocar `FECompConsultar` pidiendo ese `Numero` desde la empresa A | `COMPROBANTE_NO_ENCONTRADO` 404, sin datos del comprobante ajeno y sin revelar su existencia |
| CP-16.06 | API key válida | Invocar `FEDummy` | `Estado` = `"OK"`, `ServerTime` en ISO 8601 UTC y `Version` presente |
| CP-16.07 | Sin API key | Invocar `FEDummy` | `SOAP Fault` con `codigo` = `API_KEY_AUSENTE` y HTTP `401` |
| CP-16.08 | API key inválida | Invocar `FECompConsultar` | `SOAP Fault` con `codigo` = `API_KEY_INVALIDA` y HTTP `401` |
| CP-16.09 | API key válida | Invocar `FECompConsultar` con `TipoComprobante` inexistente | `VALIDACION` 400 dentro del sobre de error, sin consulta a la base |
| CP-16.10 | Comprobante propio | Invocar `FECompConsultar` por SOAP y consultar el mismo comprobante por REST | Los campos devueltos son los mismos: tipo, punto de venta, número, CAE, vencimiento, importes, moneda y estado |
| CP-16.11 | Comprobante propio | Invocar `FECompUltimoAutorizado` por SOAP y `GET /v1/comprobantes/ultimo-autorizado` por REST | Ambos devuelven el mismo número y el mismo CAE |
| CP-16.12 | API key válida | Invocar cualquiera de las tres operaciones | Ninguna escribe documentos: el número de comprobantes, secuencias y eventos no cambia |

## Dependencias técnicas

| Dependencia | Historia que la introduce | Detalle técnico requerido |
| --- | --- | --- |
| Endpoint SOAP, WSDL y SoapCore | US-15 | `Soap__Ruta`, `IContractFecae` y el registro del endpoint. US-16 agrega operaciones al mismo servicio, no crea otro host |
| Autenticación por API key | US-03 | `IApiKeyValidator` reutilizable desde la capa SOAP, con la misma resolución de tenant que REST |
| Casos de uso de consulta | US-09 | Servicios de consulta y DTOs `ComprobanteResponse` / último autorizado en `FacturAR.Contracts`. Los DTOs son la única fuente de los campos que devuelve el SOAP |
| Comprobantes emitidos | US-05 | Colección `comprobantes` poblada y con índices `ux_comprobantes_numeracion` e `ix_comprobantes_contribuyente_fecha` |
| Paquete NuGet nuevo | US-15 | SoapCore ya está en `FacturAR.Api`; US-16 **no** agrega paquetes |

## Notas de alcance

- No implementa todos los campos de AFIP.
- No implementa WSAA real.
- No implementa consultas masivas.
