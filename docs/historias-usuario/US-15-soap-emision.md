# US-15 — SOAP de emisión

**Épica:** Interoperabilidad  
**Prioridad:** Alta  
**Estimación:** 2 días  
**Requisito:** RF-15  
**Depende de:** US-03, US-05  
**Habilita:** US-16, US-21, US-24

## Historia

Como sistema cliente legado, quiero invocar `FECAESolicitar` por SOAP para emitir comprobantes usando un contrato compatible con el flujo de AFIP.

## Contexto

El SOAP es una fachada del mismo caso de uso de emisión. No duplica reglas ni accede a Mongo directamente. La autenticación usa la misma API key o un mecanismo equivalente definido en el contrato.

## Criterios de aceptación

- [ ] Se expone `FECAESolicitar` con un contrato XML compatible con el flujo AFIP simulado.
- [ ] El request incluye `Auth` (token y sign) o la API key según el contrato acordado.
- [ ] El servicio resuelve el contribuyente autenticado.
- [ ] El servicio reutiliza `EmitirComprobanteService`.
- [ ] La respuesta incluye `Cae`, `CaeFchVto`, `Resultado` y observaciones.
- [ ] Un error de validación devuelve un `Fault` o un resultado `R` con mensaje claro.
- [ ] Un cliente no puede emitir para otra empresa.
- [ ] La numeración y el CAE son los mismos que en REST.
- [ ] El WSDL está disponible y documentado.
- [ ] Hay tests de request exitoso, error y aislamiento.
- [ ] El SOAP no omite autenticación ni validaciones.

## Escenarios Gherkin

```gherkin
Scenario: Emisión SOAP exitosa
  Given un sistema cliente autenticado
  When invoca FECAESolicitar con una factura C válida
  Then recibe Resultado "A"
  And recibe un CAE de 14 dígitos
  And el comprobante queda persistido
```

```gherkin
Scenario: Error de validación SOAP
  Given un request con un punto de venta inexistente
  When invoca FECAESolicitar
  Then recibe un resultado "R" con el error
  And no se consume un número
```

```gherkin
Scenario: Aislamiento SOAP
  Given una API key de la empresa A
  When intenta emitir con datos de la empresa B
  Then el servicio rechaza la operación
  And no se crea el comprobante
```

## Tareas técnicas

- [ ] Definir el contrato SOAP y el WSDL.
- [ ] Configurar SoapCore o CoreWCF.
- [ ] Implementar `FECAESolicitar`.
- [ ] Resolver autenticación y tenant.
- [ ] Reutilizar el caso de uso de emisión.
- [ ] Mapear errores a `Fault` o resultado `R`.
- [ ] Publicar el WSDL.
- [ ] Agregar tests de contrato y aislamiento.
- [ ] Documentar un ejemplo de request y response.

## Definition of Done

- [ ] `FECAESolicitar` emite un comprobante válido.
- [ ] El SOAP y el REST producen el mismo resultado funcional.
- [ ] El WSDL se puede consumir desde SoapUI o Postman.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el manual.

## Contrato técnico

### Modelo de datos

US-15 **no agrega colecciones ni índices**. Reusa sin cambios:

| Colección | Uso desde SOAP | Índice relevante |
| --- | --- | --- |
| `comprobantes` | Persistencia del comprobante emitido | `ux_comprobantes_cae`, `ux_comprobantes_numeracion`, `ux_comprobantes_idempotencia` |
| `secuencias` | Numeración atómica | `ux_secuencias_contribuyente_punto_tipo` |
| `puntosVenta` | Validación del punto de venta | `ux_puntos_venta_contribuyente_numero` |

El `ContribuyenteId` se resuelve **siempre** desde la API key autenticada. El XML del request **no** puede portar `contribuyenteId`, `cuit` del emisor ni `razonSocial` del emisor como fuente de tenant: esos campos del contrato AFIP (`Cuit`, `RazonSocial`) se **verifican** contra el contribuyente autenticado y la diferencia produce el rechazo de aislamiento.

### Operaciones SOAP

Contrato: `FECAESolicitar`, `FECAEGenerar`. Namespace XML: `http://ar.gov.afip.dif.FEV1Service/`. Binding: SOAP 1.1 sobre HTTP POST. El endpoint se publica en `/soap` y el WSDL en `/soap?wsdl`.

| Operación | Ruta / método | Autenticación | Request | Response | Errores |
| --- | --- | --- | --- | --- | --- |
| `FECAESolicitar` | `POST /soap` (`SOAPAction: .../FECAESolicitar`) | API key en el header HTTP `X-Api-Key`; alternativamente `Auth/Token` dentro del sobre | `FECAESolicitarRequest`: `Auth` (`Token`, `Sign`), `Cuit`, `PtoVta`, `TipoComprobante`, `Concepto`, `DocTipo`, `DocNro`, `CondicionIVA`, `Items[]` (`Descripcion`, `Cantidad`, `PrecioUnitario`, `AlicuotaIVA`), `IdempotencyKey` (extensión opcional) | `FECAESolicitarResponse` → `FeCabResp` con `Resultado` (`"A"`/`"R"`), `CodAutorizacion` (CAE), `FchVto`, `Observaciones` | `VALIDACION` 400, `EMISION_INVALIDA` 400, `PUNTO_VENTA_INACTIVO` 409, `IDEMPOTENCIA_CONFLICTO` 409, `API_KEY_AUSENTE` 401, `API_KEY_INVALIDA` 401, `MONGO_NO_DISPONIBLE` 503, `ERROR_INTERNO` 500 |
| `FECAEGenerar` | `POST /soap` (`SOAPAction: .../FECAEGenerar`) | Igual que `FECAESolicitar` | `FECAEGenerarRequest`: misma estructura de cabecera y ítems | `FECAEGenerarResponse` → `Respuesta` con `Resultado` (`"A"`/`"R"`), `Error` (código canónico), `Observaciones` (mensaje) | `VALIDACION` 400, `EMISION_INVALIDA` 400, `PUNTO_VENTA_INACTIVO` 409, `API_KEY_AUSENTE` 401, `API_KEY_INVALIDA` 401, `ERROR_INTERNO` 500 |

Reglas de operación:

- **Solo `FECAESolicitar` persiste.** `FECAEGenerar` es la superficie de diagnóstico: ejecuta las mismas validaciones del caso de uso de emisión y devuelve el resultado controlado **sin** insertar comprobante, sin consumir número y sin generar CAE. Un cliente nunca debe llamar `FECAEGenerar` esperando una emisión.
- **Reutilización de reglas.** `FECAESolicitar` delega íntegramente en `EmitirComprobanteService` (Application). El SOAP **no** recalcula importes, no genera CAE, no numera y no valida alícuotas por su cuenta. `FECAEGenerar` reutiliza el mismo camino de validación a través de ese servicio.
- **Numeración y CAE idénticos a REST.** El comprobante emitido por SOAP es el mismo documento que emite `POST /v1/comprobantes`: mismo `PuntoVenta`, `Numero`, `Cae`, `CaeVencimiento`, `ImporteNeto`, `ImporteIva`, `ImporteTotal` y `Moneda`.
- **Aislamiento.** El `Cuit` del request se compara con el CUIT del contribuyente autenticado. Si difieren, la operación se rechaza con el sobre de error (`Resultado` `"R"` / `Fault`) **sin** crear comprobante y **sin** revelar si ese CUIT existe en otra empresa.
- **Idempotencia.** SOAP no transporta el header `Idempotency-Key`. La extensión opcional `IdempotencyKey` del request se mapea al mismo mecanismo de US-06 (`ContribuyenteId` + `IdempotencyKey` + hash de payload). Si se omite, un reintento genera un comprobante nuevo: es responsabilidad del cliente enviar la clave.

### Sobre el envelope `R` / `Fault`

El contrato distingue dos planos de error, igual que el flujo AFIP:

1. **Error de negocio → sobre `R` (HTTP 200).** La respuesta es un `FeCabResp`/`Respuesta` con `Resultado = "R"`. `Observaciones` lleva el código canónico y el mensaje, separados por `:`, por ejemplo `EMISION_INVALIDA: El comprobante a emitir no supera las reglas de negocio.`. El HTTP se mantiene en `200` porque el transporte SOAP fue correcto.
2. **Fallo de protocolo o de autenticación → `SOAP Fault`.** Se usa cuando el sobre no se puede deserializar, el WSDL no aplica o la API key no se puede resolver. El `faultstring` incluye el código canónico y el `detail` lo separa en `codigo` y `mensaje`:

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
    <soap:Fault>
      <faultcode>soap:Client</faultcode>
      <faultstring>API_KEY_AUSENTE: Falta el header X-Api-Key.</faultstring>
      <detail>
        <codigo>API_KEY_AUSENTE</codigo>
        <mensaje>Falta el header X-Api-Key.</mensaje>
      </detail>
    </soap:Fault>
  </soap:Body>
</soap:Envelope>
```

Reglas transversales del sobre:

- El `codigo` del error es **siempre** uno de la lista canónica de `docs/contratos-api.md` §2.1. El SOAP **no** define códigos propios.
- El `mensaje` es para humanos y puede cambiar sin romper clientes.
- Nunca se devuelve `500` por datos inválidos del cliente: el fallo de negocio viaja en el sobre `R`.
- Un `ERROR_INTERNO` 500 se representa como `Fault` con `faultcode` `soap:Server` y **sin** detalle interno en el mensaje.

### Autenticación

- **Canónica:** header HTTP `X-Api-Key`, idéntico a REST. Reutiliza el servicio de validación de US-03, no una implementación paralela.
- **Alternativa (contrato AFIP):** `Auth/Token` dentro del sobre. En el simulador `Token` transporta la API key y `Sign` es opcional e ignorado. No hay WSAA ni certificados.
- Si falta la credencial: `API_KEY_AUSENTE` 401. Si existe pero no corresponde o está revocada: `API_KEY_INVALIDA` 401. El fallo llega como `Fault` (fallo de protocolo/autenticación), no como sobre `R`.
- La API key nunca viaja en el `Observaciones`, en el `Fault` ni en el log. Solo se registra el `Prefijo` visible (`fak_<prefix>`).

### Biblioteca SOAP

Se usa **`SoapCore`** como middleware SOAP sobre ASP.NET Core.

| Criterio | SoapCore | CoreWCF |
| --- | --- | --- |
| Licencia | MIT | MIT |
| Modelo de hosting | Middleware de ASP.NET Core, registrable desde el pipeline de Minimal API | Hosting estilo WCF (`ServiceHost`, `ServiceBehavior`, `OperationBehavior`) y configuración propia |
| Contrato | Interfaces con `[ServiceContract]`/`[OperationContract]` y DTOs con atributos XML | Atributos WCF, `MessageContract`, `DataContract` |
| Genera WSDL | Sí, en la ruta del endpoint (`/soap?wsdl`) | Sí, con más configuración de metadatos |
| Dependencias | `System.ServiceModel.Http` (trae `System.ServiceModel.Primitives`) | Stack `System.ServiceModel.*` más amplio |
| Encaje con el stack | Alto: el proyecto de API es Minimal API y comparte DI, middleware de auth y OpenAPI | Bajo: obliga a un segundo modelo de hosting y de configuración dentro del mismo proceso |

**Decisión: SoapCore.** CoreWCF está pensado para migrar servicios WCF existentes y aporta un modelo de hosting y de configuración que el proyecto no tiene ni necesita; para publicar un WSDLAFIP-compatible sobre el pipeline actual de Minimal API, SoapCore es la opción más liviana y con menos dependencias.

**Es un paquete NuGet nuevo** y debe justificarse en el PR:

- Se agrega **solo** a `FacturAR.Api`, que es el único proyecto que hospeda el endpoint.
- `System.ServiceModel.Primitives` (origen de `[ServiceContract]`/`[OperationContract]`) llega **transitivamente** con `SoapCore`: no se agrega una referencia NuGet separada.
- No se agrega ningún paquete a `FacturAR.Domain`, `FacturAR.Application`, `FacturAR.Infrastructure` ni `FacturAR.Contracts`.
- Versión a fijar: la última estable 1.2.x disponible al momento de implementar, fijada en el `.csproj` (no flotante).

### Casos de uso y servicios

| Pieza | Capa | Responsabilidad |
| --- | --- | --- |
| `IContractFecae` (interfaz del contrato SOAP) | `FacturAR.Api/Soap/` | Declarar `[ServiceContract]` y los `[OperationContract]` que generan el WSDL |
| DTOs XML (`FECAESolicitarRequest`, `FeCabResp`, `Respuesta`, etc.) | `FacturAR.Contracts/Soap/` | Contrato serializado con `XmlSerializer`. Solo atributos de BCL (`System.Xml.Serialization`): `Contracts` **no** referencia SoapCore ni `System.ServiceModel` |
| `FecaeMapeador` | `FacturAR.Api/Soap/` | Traduce el request XML al `EmitirComprobanteRequest` de `FacturAR.Contracts` y la respuesta del caso de uso al sobre AFIP. Vive en la capa API porque el shape XML es un detalle de transporte |
| `EmitirComprobanteService` | `FacturAR.Application` (US-05) | Reglas de emisión, numeración, CAE e idempotencia. **No se modifica** |
| `IApiKeyValidator` / `ApiKeyService` | `FacturAR.Application` (US-03) | Resolver la API key a `ContribuyenteId`. Debe quedar **expuesto como servicio**, no como filtro de endpoint, para que SOAP pueda reutilizarlo |

`Contracts` sigue siendo el único lugar con DTOs públicos. El SOAP no expone entidades de `Domain` ni campos internos (`IdempotencyKey` como metadato, `ContribuyenteId`, hashes).

### Configuración

| Clave | Tipo | Default | Uso |
| --- | --- | --- | --- |
| `Soap__Habilitado` | `bool` | `true` | Permite no publicar el endpoint SOAP en un ambiente |
| `Soap__Ruta` | `string` | `/soap` | Ruta del endpoint. El WSDL se deriva de esta ruta (`{ruta}?wsdl`) |

Los valores se leen con el prefijo `Soap__` y se pueden sobrescribir por variable de entorno sin recompilar. No hay secretos en esta sección.

### Integración con otras historias

| Historia | Relación |
| --- | --- |
| US-03 | Aporta la autenticación por API key. Requiere que la validación quede como servicio reutilizable |
| US-05 | Aporta `EmitirComprobanteService` y los DTOs de emisión. Sus reglas se reutilizan sin duplicar |
| US-06 | Aporta el mecanismo de idempotencia que se mapea a la extensión `IdempotencyKey` del sobre |
| US-07 | Aporta la numeración atómica que el SOAP consume sin cambios |
| US-16 | Comparte el mismo endpoint y el mismo WSDL: agrega operaciones de consulta |
| US-21 | Debe incluir el WSDL y ejemplos de request/response en la documentación y en la colección Postman |
| US-24 (stretch) | El contrato XML desacopla el simulador del futuro adaptador AFIP real |

### Documentación a sincronizar

- `docs/contratos-api.md`: agregar la sección de SOAP (ruta, WSDL, operaciones, forma del sobre `R`/`Fault` y autenticación).
- `docs/manual-integracion.md`: sección de interoperabilidad SOAP con un ejemplo de request y response.
- `README.md`: agregar `/soap` y `/soap?wsdl` a la tabla de endpoints de la API.

## Casos de prueba

| ID | Precondición | Pasos | Resultado esperado |
| --- | --- | --- | --- |
| CP-15.01 | API levantada | Solicitar `GET /soap?wsdl` | `200` con un WSDL válido que declara `FECAESolicitar` y `FECAEGenerar` y el namespace `http://ar.gov.afip.dif.FEV1Service/` |
| CP-15.02 | API key válida de la empresa A | Invocar `FECAESolicitar` con una factura C de un ítem | `200`, `Resultado` = `"A"`, `CodAutorizacion` con 14 dígitos, `FchVto` a 10 días de la emisión y el comprobante persistido con el mismo `PuntoVenta`/`Numero` |
| CP-15.03 | API key válida | Invocar `FECAESolicitar` con un `PtoVta` inexistente | `Resultado` = `"R"`, `Observaciones` con el código canónico, y **no** se consume número ni se genera CAE |
| CP-15.04 | Sin API key | Invocar `FECAESolicitar` | `SOAP Fault` con `codigo` = `API_KEY_AUSENTE` y HTTP `401` |
| CP-15.05 | API key revocada o inexistente | Invocar `FECAESolicitar` | `SOAP Fault` con `codigo` = `API_KEY_INVALIDA` y HTTP `401`; el `Fault` no incluye el valor de la key |
| CP-15.06 | API key de la empresa A | Invocar `FECAESolicitar` con el `Cuit` de la empresa B | Operación rechazada con sobre de error y **no** se crea comprobante; el mensaje no revela si ese CUIT existe |
| CP-15.07 | API key válida | Invocar `FECAEGenerar` con un comprobante inválido | `Respuesta` con `Resultado` = `"R"`, `Error` = código canónico y `Observaciones` con el mensaje; **no** se persiste comprobante ni se consume número |
| CP-15.08 | API key válida y `IdempotencyKey` repetida con el mismo payload | Invocar `FECAESolicitar` dos veces | Ambas respuestas traen el mismo `Numero` y el mismo `Cae`; no se genera un segundo comprobante |
| CP-15.09 | API key válida y `IdempotencyKey` repetida con payload distinto | Invocar `FECAESolicitar` | `IDEMPOTENCIA_CONFLICTO` 409 dentro del sobre de error; no se crea comprobante |
| CP-15.10 | Mismo payload emitido por REST y por SOAP, con keys de idempotencia distintas | Comparar ambos comprobantes | `TipoComprobante`, `PuntoVenta`, `Numero`, `Cae`, `CaeVencimiento`, `ImporteNeto`, `ImporteIva`, `ImporteTotal` y `Moneda` son los mismos |
| CP-15.11 | Sobre SOAP mal formado (XML inválido) | Enviar el request | `SOAP Fault` con `faultcode` `soap:Client`; no se registra ninguna operación de negocio |
| CP-15.12 | MongoDB no disponible | Invocar `FECAESolicitar` | `SOAP Fault` con `codigo` = `MONGO_NO_DISPONIBLE` y HTTP `503`; no se consume número |
| CP-15.13 | SoapCore registrado | Inspeccionar la respuesta del WSDL | El WSDL no expone DTOs internos ni campos de `Domain` (`ContribuyenteId`, `PasswordHash`) |

## Dependencias técnicas

| Dependencia | Historia que la introduce | Detalle técnico requerido |
| --- | --- | --- |
| Autenticación por API key como servicio | US-03 | `IApiKeyValidator` (Application) que resuelva la key a `ContribuyenteId` y actualice `UltimoUso`. No puede ser un filtro exclusivo de Minimal API, porque SOAP no es un endpoint de Minimal API |
| Caso de uso de emisión | US-05 | `EmitirComprobanteService` en Application y `EmitirComprobanteRequest`/`ComprobanteResponse` en `FacturAR.Contracts`. El SOAP no debe contener reglas de emisión propias |
| Idempotencia | US-06 | Mecanismo `(ContribuyenteId, IdempotencyKey)` con hash de payload, reutilizado por la extensión `IdempotencyKey` |
| Numeración atómica | US-07 | `Secuencia` con incremento atómico; el SOAP no asigna números en memoria |
| Serialización de `Guid` y `decimal` | Existente | `MongoSerialization.Configure()` ya registra `GuidRepresentation.Standard`. Los importes viajan como `decimal` con 2 decimales |
| Build y hosting del proyecto de API | US-19, US-20 | El paquete SoapCore debe quedar en la imagen de Docker y el build de CI |
| Paquete NuGet nuevo | Esta historia | `SoapCore` solo en `FacturAR.Api`, versión fijada y justificada en el PR. `System.ServiceModel.Primitives` llega transitivamente |

## Notas de alcance

- No implementa WSAA real ni certificados AFIP.
- No implementa todos los campos de AFIP.
- No implementa SOAP de consultas; eso corresponde a US-16.
