# Manual de integración

Versión del contrato: `0.1.0` | Última actualización: `2026-11-07`

## Alcance

Este manual describe la **API pública para integradores**. No documenta la operación interna del portal, la consola global ni la infraestructura del proyecto.

Qué vas a encontrar: cómo solicitar acceso, activar tu cuenta, crear una API key, autenticar requests, emitir comprobantes, consultarlos, descargar el PDF, usar idempotencia, verificar un comprobante por su CAE y emitir o consultar por SOAP.

> El CAE es simulado y los comprobantes no tienen validez fiscal. FacturAR no se conecta con AFIP/ARCA.

El contrato de la API es normativo: si este manual y el contrato se contradicen, manda el contrato.

## Endpoints para integradores

Base URL local: `http://localhost:5171`. En producción, usá la URL que te indique el equipo de FacturAR.

| Método | Ruta | Qué hace |
| --- | --- | --- |
| `POST` | `/v1/solicitudes-alta` | Solicitud pública de alta de empresa, con rate limit. |
| `GET` | `/v1/invitaciones/{token}` | Valida el token de invitación. |
| `POST` | `/v1/invitaciones/{token}/aceptar` | Activa la cuenta y define la contraseña. |
| `POST` | `/v1/auth/login` | Login de empresa; devuelve la cookie de sesión. |
| `POST` | `/v1/auth/logout` | Cierra la sesión de empresa. |
| `GET` | `/v1/auth/me` | Datos del usuario de empresa autenticado. |
| `GET` | `/v1/comprobantes/verificar/{cae}` | Verificación pública de un comprobante por CAE. No requiere autenticación. |
| `GET` | `/v1/api-keys` | Lista las API keys de la empresa. Requiere sesión de empresa con rol `Administrador`. |
| `POST` | `/v1/api-keys` | Crea una key y devuelve el valor completo una única vez. Requiere rol `Administrador`. |
| `DELETE` | `/v1/api-keys/{id}` | Revoca una key. Requiere rol `Administrador`. |
| `POST` | `/v1/api-keys/{id}/regenerar` | Regenera el secreto y devuelve el valor nuevo una única vez. Requiere rol `Administrador`. |
| `POST` | `/v1/comprobantes` | Emisión de facturas A, B y C con CAE simulado. Requiere API key o sesión de empresa (`Administrador`/`Emisor`) e `Idempotency-Key`. |
| `POST` | `/v1/comprobantes/presupuesto` | Calcula neto, IVA y total sin emitir. Requiere sesión de empresa (`Administrador`/`Emisor`). |
| `GET` | `/v1/puntos-venta` | Puntos de venta de la empresa para el formulario de emisión. Requiere sesión de empresa (`Administrador`/`Emisor`). |
| `POST` | `/soap` | Fachada SOAP 1.1: `FECAESolicitar`, `FECAEGenerar`, `FECompUltimoAutorizado`, `FECompConsultar` y `FEDummy`. Requiere API key. |
| `GET` | `/soap?wsdl` | WSDL de la fachada SOAP. |
| `GET` | `/v1/comprobantes` | Lista los comprobantes de la empresa autenticada, con filtros y paginación. |
| `GET` | `/v1/comprobantes/{id}` | Detalle de un comprobante propio, con sus ítems. |
| `GET` | `/v1/comprobantes/{id}/pdf` | PDF del comprobante con sus datos y el QR de verificación. |
| `GET` | `/v1/comprobantes/ultimo-autorizado` | Último número autorizado por tipo y punto de venta. |
| `GET` | `/v1/comprobantes/dashboard` | Métricas del mes y últimos cinco comprobantes de la empresa autenticada. |

### Headers

| Header | Dónde | Regla |
| --- | --- | --- |
| `X-Api-Key` | Endpoints de negocio | Obligatorio. Formato `^fak_[0-9a-f]{12}_[A-Za-z0-9_-]{43}$`, máximo 200 caracteres. Si falta: `401 API_KEY_AUSENTE`. Si no existe, está revocada, el hash no coincide o la empresa está inactiva: `401 API_KEY_INVALIDA`. |
| `Idempotency-Key` | `POST /v1/comprobantes` | Obligatorio. Máximo 128 caracteres. Alcance `(empresa, clave)`. El reintento con la misma clave y el mismo payload devuelve `200` con el comprobante original; un payload distinto responde `409 IDEMPOTENCIA_CONFLICTO`. No expira. |

### Códigos de error

Los códigos son parte del contrato público y un cliente puede hacer switch sobre ellos.

| Código | HTTP | Cuándo |
| --- | --- | --- |
| `VALIDACION` | `400` | El cuerpo o un parámetro no cumple el contrato. |
| `CUIT_INVALIDO` | `400` | El CUIT no supera la validación de dígito verificador. |
| `CAE_INVALIDO` | `400` | El CAE no tiene 14 dígitos válidos. |
| `PASSWORD_DEBIL` | `400` | La contraseña no cumple la política. |
| `EMISION_INVALIDA` | `400` | El comprobante no supera las reglas de negocio. |
| `TIPO_NOTA_INCOMPATIBLE` | `400` | Una nota apunta a un comprobante de otro tipo base. |
| `IMPORTE_EXCEDIDO` | `400` | El importe de la nota supera el saldo ajustable. |
| `API_KEY_AUSENTE` | `401` | No se envió el header `X-Api-Key`. |
| `API_KEY_INVALIDA` | `401` | La API key no existe, está revocada o el hash no coincide. |
| `CREDENCIALES_INVALIDAS` | `401` | Email o contraseña incorrectos, o la cuenta está inactiva. |
| `SIN_PERMISOS` | `403` | El rol no alcanza para la operación. |
| `CUENTA_INACTIVA` | `403` | El contribuyente o el usuario está desactivado. |
| `EMPRESA_NO_ASOCIADA` | `403` | El usuario no tiene empresa activa asociada. |
| `INVITACION_NO_ENCONTRADA` | `404` | No existe la invitación. |
| `COMPROBANTE_NO_ENCONTRADO` | `404` | No existe un comprobante con el CAE o el id indicado. |
| `SOLICITUD_PENDIENTE` | `409` | Ya existe una solicitud pendiente con el mismo CUIT o email. |
| `INVITACION_USADA` | `409` | El token de invitación ya fue aceptado. |
| `IDEMPOTENCIA_CONFLICTO` | `409` | La misma `Idempotency-Key` llegó con un payload diferente. |
| `COMPROBANTE_ANULADO` | `409` | El comprobante está anulado y no admite la operación. |
| `PUNTO_VENTA_INACTIVO` | `409` | El punto de venta no está activo. |
| `INVITACION_VENCIDA` | `410` | El token de invitación superó las 48 horas. |
| `RATE_LIMIT` | `429` | Se superó el límite de peticiones del endpoint. |
| `ERROR_INTERNO` | `500` | Falla inesperada. El detalle queda en el log. |

## Qué es FacturAR

FacturAR es un simulador de facturación electrónica argentina. Permite solicitar el alta de una empresa, activar la cuenta, gestionar API keys, emitir y consultar comprobantes, descargar el PDF, verificar un comprobante por CAE y usar REST o SOAP.

> Importante: el CAE es simulado y los comprobantes no tienen validez fiscal. No se realizan llamadas reales a AFIP/ARCA.

## URLs

| Entorno | API | Portal |
| --- | --- | --- |
| Local | `http://localhost:5171` | `http://localhost:5116` |

## 1. Solicitar acceso

1. Ingresar al portal y abrir la solicitud de alta de empresa (`/solicitar-alta`).
2. Completar razón social, CUIT, condición frente al IVA, datos del solicitante, email, teléfono y domicilio.
3. Aceptar los términos y enviar la solicitud.
4. Recibir el email de confirmación.

La solicitud queda pendiente de revisión. No se crea la empresa ni la cuenta en ese momento. El equipo de FacturAR revisa la información y, si la aprueba, crea la empresa, el punto de venta `1 Casa Central` y una invitación para el email del solicitante.

Errores del flujo: `400 VALIDACION` y `400 CUIT_INVALIDO` por datos inválidos, `409 SOLICITUD_PENDIENTE` si ya hay una solicitud pendiente con el mismo CUIT o email, y `429 RATE_LIMIT` por exceso de peticiones.

## 2. Activar la cuenta

1. Abrir el link del email de invitación.
2. Verificar la razón social y el email.
3. Definir una contraseña segura.
4. Confirmar la activación.
5. Iniciar sesión con el email y la contraseña.

La invitación vence a las 48 horas, es de un solo uso y solo es válida para el email destinatario. Si vence o se pierde, el equipo de FacturAR puede reenviarla.

Política de contraseña:

- Mínimo 10 caracteres y máximo 128.
- Al menos una letra y un número.
- No se aceptan contraseñas triviales ni predecibles.
- La contraseña nunca se devuelve por la API.

Errores del flujo: `404 INVITACION_NO_ENCONTRADA`, `410 INVITACION_VENCIDA`, `409 INVITACION_USADA`, `400 PASSWORD_DEBIL` y `400 VALIDACION` si la confirmación no coincide.

## 3. Crear una API key

1. Iniciar sesión en el portal como administrador de la empresa.
2. Abrir la sección API keys (`/api-keys`).
3. Crear una key con un nombre descriptivo, por ejemplo `Ecommerce producción`.
4. Copiar el valor completo en ese momento.

El valor completo se muestra una sola vez. Después solo se ve el prefijo. Si se pierde, se regenera o se crea otra key. La key pertenece a una única empresa y no se comparte entre empresas ni se envía por email.

Formato de referencia:

```text
fak_<prefijo>_<secreto>
```

La expresión regular de validación es `^fak_[0-9a-f]{12}_[A-Za-z0-9_-]{43}$`.

- `fak` identifica una key de FacturAR.
- `<prefijo>` son los 12 caracteres hexadecimales del prefijo visible.
- `<secreto>` son los 43 caracteres base64url de la parte aleatoria, que no se puede recuperar.

FacturAR nunca devuelve el valor completo después de crearlo. Solo conserva el prefijo visible y un hash del secreto.

La key pertenece a una única empresa. No se comparte entre empresas ni se envía por email.

## 4. Autenticar requests

Enviar la API key en el header `X-Api-Key`:

```http
GET /v1/comprobantes HTTP/1.1
Host: localhost:5171
X-Api-Key: fak_<prefijo>_<secreto>
Accept: application/json
```

Formato: `fak_` + 12 caracteres hexadecimales del prefijo + `_` + 43 caracteres base64url del secreto. La expresión regular es `^fak_[0-9a-f]{12}_[A-Za-z0-9_-]{43}$` y el valor admitido tiene como máximo 200 caracteres.

Reglas:

- Sin header, o con el header vacío: `401 API_KEY_AUSENTE`.
- Key mal formada, inexistente, revocada o de un contribuyente inactivo: `401 API_KEY_INVALIDA`. El mensaje es el mismo en todos los casos: la respuesta no revela si el prefijo existe.
- La key resuelve la empresa. La empresa sale **solo** de la credencial; nunca se envía en el body ni en el query string y, si se envía, se ignora.
- La API key no autoriza los endpoints de sesión de empresa; esos usan la cookie del portal.
- La verificación pública por CAE (`GET /v1/comprobantes/verificar/{cae}`) permanece anónima y no requiere API key.

Ejemplo de error de autenticación:

```json
{
  "codigo": "API_KEY_AUSENTE",
  "mensaje": "Se requiere el header X-Api-Key."
}
```

La API registra el último uso de la key para mostrarlo en el portal. Si ese registro falla, la respuesta de negocio no se bloquea.

## 5. Emitir una factura


| Header | Regla |
| --- | --- |
| `X-Api-Key` | Obligatorio. Si falta: `401 API_KEY_AUSENTE`. Si no existe, está revocada o la empresa está inactiva: `401 API_KEY_INVALIDA`. |
| `Idempotency-Key` | Obligatorio, máximo 128 caracteres. Se persiste y es única por empresa. Usar una clave distinta por operación; si se repite con el mismo body, la API devuelve el comprobante original. |

```http
POST /v1/comprobantes HTTP/1.1
Host: localhost:5171
X-Api-Key: fak_<prefijo>_<secreto>
Idempotency-Key: pedido-2026-0001
Content-Type: application/json
```

```json
{
  "puntoVenta": 1,
  "tipoComprobante": 11,
  "receptor": {
    "documentoTipo": "99",
    "documentoNumero": "0",
    "razonSocial": "Consumidor Final",
    "condicionIva": 5
  },
  "items": [
    {
      "descripcion": "Curso de programación",
      "cantidad": 1,
      "precioUnitario": 10000,
      "alicuotaIva": 21
    }
  ],
  "observaciones": "Venta de prueba"
}
```

- `tipoComprobante`: facturas `1` (A), `6` (B) u `11` (C), y notas de débito `2`, `7`, `12` o de crédito `3`, `8`, `13` (ver «Notas de crédito y débito»).
- `puntoVenta`: obligatorio y positivo. Debe existir y estar activo para la empresa autenticada.
- `fechaEmision`: opcional, en UTC. Si no viene, se usa el momento de la emisión.
- `receptor`: `documentoTipo`, `documentoNumero`, `razonSocial` y `condicionIva` (`1` responsable inscripto, `4` exento, `5` consumidor final, `6` monotributo). La factura A exige CUIT válido y responsable inscripto; la B admite consumidor final, monotributo o exento; la C exige consumidor final y acepta `documentoTipo` `99` o `CUIT` con CUIT válido.
- `items`: entre 1 y 500. `cantidad` mayor que cero, `precioUnitario` no negativo, `alicuotaIva` en `0`, `5`, `10.5`, `21` o `27`.
- `importeTotal`: opcional. Si viene, debe coincidir con el total calculado.
- `observaciones`: opcional, hasta 500 caracteres.

Respuesta `201` (`FacturAR.Contracts.Comprobantes.ComprobanteResponse`):

```json
{
  "id": "4a4f3f4e-7d7a-4f4e-9a5e-2f6b8c9d0e1f",
  "tipoComprobante": 11,
  "tipoComprobanteDescripcion": "Factura C",
  "puntoVenta": 1,
  "numero": 1,
  "fechaEmision": "2026-10-25T15:30:00Z",
  "cae": "12345678901234",
  "caeVencimiento": "2026-11-04T00:00:00Z",
  "receptor": {
    "documentoTipo": "99",
    "documentoNumero": "0",
    "razonSocial": "Consumidor Final",
    "condicionIva": 5
  },
  "importeNeto": 10000,
  "importeIva": 0,
  "importeTotal": 10000,
  "moneda": "ARS",
  "estado": "Emitido"
}
```

Códigos de tipo de comprobante:

| Código | Comprobante |
| --- | --- |
| `1` | Factura A |
| `2` | Nota de Débito A |
| `3` | Nota de Crédito A |
| `6` | Factura B |
| `7` | Nota de Débito B |
| `8` | Nota de Crédito B |
| `11` | Factura C |
| `12` | Nota de Débito C |
| `13` | Nota de Crédito C |

Reglas de importes:

- `subtotal = cantidad × precioUnitario`.
- En Factura A el IVA se discrimina: `importeIva = subtotal × alicuotaIva / 100` y `importeTotal = importeNeto + importeIva`.
- En Factura B el IVA se incluye y no se discrimina: `importeIva = 0` y `importeTotal = importeNeto`.
- En Factura C no se discrimina IVA: `importeIva = 0` y `importeTotal = importeNeto`.
- Alícuotas admitidas: `0`, `5`, `10,5`, `21`, `27`.
- El cálculo es con `decimal` y se redondea a 2 decimales.

La emisión asigna número y CAE dentro de una transacción. El número es correlativo por empresa, punto de venta y tipo. El CAE tiene 14 dígitos y vence a los 10 días.

El punto de venta debe existir y estar activo. Si no existe o pertenece a otra empresa, responde `404 COMPROBANTE_NO_ENCONTRADO`; si existe pero está inactivo, responde `409 PUNTO_VENTA_INACTIVO`.

| Código | HTTP | Cuándo ocurre |
| --- | --- | --- |
| `VALIDACION` | `400` | Falta `Idempotency-Key`, supera 128 caracteres, o el body no cumple las reglas de tipo, receptor, ítems, observaciones o `importeTotal`. |
| `EMISION_INVALIDA` | `400` | El emisor consumidor final intenta emitir una factura o nota A. |
| `CUIT_INVALIDO` | `400` | El CUIT o documento del receptor no supera la validación. |
| `API_KEY_AUSENTE` | `401` | Falta el header `X-Api-Key`. |
| `API_KEY_INVALIDA` | `401` | La key no existe, está revocada, el hash no coincide o la empresa está inactiva. |
| `CUENTA_INACTIVA` | `403` | El contribuyente autenticado está inactivo. |
| `SIN_PERMISOS` | `403` | El rol del principal no permite emitir comprobantes. |
| `TIPO_NOTA_INCOMPATIBLE` | `400` | La nota no es de la misma letra que la factura original, o referencia otra nota. |
| `IMPORTE_EXCEDIDO` | `400` | La nota de crédito supera el saldo ajustable de la factura original. |
| `COMPROBANTE_NO_ENCONTRADO` | `404` | El punto de venta, o el comprobante original de una nota, no existe o pertenece a otra empresa. |
| `COMPROBANTE_ANULADO` | `409` | El comprobante original de una nota está anulado o rechazado. |
| `PUNTO_VENTA_INACTIVO` | `409` | El punto de venta existe y pertenece a la empresa, pero está inactivo. |
| `IDEMPOTENCIA_CONFLICTO` | `409` | La misma `Idempotency-Key` llegó con un payload distinto. |
| `ERROR_INTERNO` | `500` | Falla inesperada; el detalle queda en el log y no en la respuesta. |

### 5.1 Emitir por SOAP


La fachada SOAP usa **SOAP 1.1** sobre `POST /soap`; el WSDL se publica en `GET /soap?wsdl`. El namespace es `http://ar.gov.afip.dif.FEV1Service/`. Es una fachada del mismo caso de uso de emisión: no duplica reglas, numeración ni generación de CAE.

Autenticación: la misma API key de REST, en el header `X-Api-Key` o en `Auth/Token` dentro del sobre (`Auth/Sign` es opcional e ignorado). La empresa sale solo de la key; el `Cuit` del request se compara con el de la empresa autenticada.

```http
POST /soap HTTP/1.1
Host: localhost:5171
Content-Type: text/xml; charset=utf-8
SOAPAction: http://ar.gov.afip.dif.FEV1Service/FECAESolicitar
X-Api-Key: fak_<prefijo>_<secreto>
```

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
    <FECAESolicitar xmlns="http://ar.gov.afip.dif.FEV1Service/">
      <request>
        <Cuit>20123456786</Cuit>
        <PtoVta>1</PtoVta>
        <TipoComprobante>11</TipoComprobante>
        <Concepto>1</Concepto>
        <DocTipo>99</DocTipo>
        <DocNro>0</DocNro>
        <CondicionIVA>5</CondicionIVA>
        <Items>
          <Item>
            <Descripcion>Servicio</Descripcion>
            <Cantidad>1</Cantidad>
            <PrecioUnitario>100</PrecioUnitario>
            <AlicuotaIVA>0</AlicuotaIVA>
          </Item>
        </Items>
        <IdempotencyKey>pedido-soap-2026-0001</IdempotencyKey>
      </request>
    </FECAESolicitar>
  </soap:Body>
</soap:Envelope>
```

Respuesta aprobada:

```xml
<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
  <s:Body>
    <FECAESolicitarResponse xmlns="http://ar.gov.afip.dif.FEV1Service/">
      <FeCabResp>
        <Resultado>A</Resultado>
        <CodAutorizacion>12345678901234</CodAutorizacion>
        <FchVto>20261113</FchVto>
      </FeCabResp>
    </FECAESolicitarResponse>
  </s:Body>
</s:Envelope>
```

`Resultado` es `"A"` o `"R"`. El CAE (`CodAutorizacion`) tiene 14 dígitos y `FchVto` usa `yyyyMMdd`. La numeración y el CAE son los mismos que genera `POST /v1/comprobantes`.

Error de negocio: HTTP `200` con `Resultado = "R"` y el código canónico en `Observaciones`:

```xml
<FECAESolicitarResponse xmlns="http://ar.gov.afip.dif.FEV1Service/">
  <FeCabResp>
    <Resultado>R</Resultado>
    <Observaciones>PUNTO_VENTA_INACTIVO: El punto de venta está inactivo.</Observaciones>
  </FeCabResp>
</FECAESolicitarResponse>
```

Error de autenticación o protocolo: `SOAP Fault` con el HTTP del código. La API key no aparece en el `Fault`:

```xml
<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
  <s:Body>
    <s:Fault>
      <faultcode>s:Client</faultcode>
      <faultstring>API_KEY_AUSENTE: Falta el header X-Api-Key.</faultstring>
      <detail>
        <codigo>API_KEY_AUSENTE</codigo>
        <mensaje>Falta el header X-Api-Key.</mensaje>
      </detail>
    </s:Fault>
  </s:Body>
</s:Envelope>
```

`FECAEGenerar` es diagnóstico: ejecuta las mismas validaciones que `FECAESolicitar`, pero **no** persiste el comprobante, no consume número y no genera CAE. Devuelve `Respuesta` con `Resultado`, `Error` y `Observaciones`. No usarlo esperando una emisión.

Límites conocidos:

- El contrato XML no transporta `RazonSocial` del receptor. Una Factura C se emite de punta a punta; las Facturas A y B se rechazan con `VALIDACION` por SOAP.
- `IdempotencyKey` es una extensión opcional. Si se omite, cada reintento es una emisión nueva. La fachada reutiliza la idempotencia de la idempotencia: con la misma clave y el mismo payload devuelve el mismo comprobante, y con un payload distinto rechaza con `IDEMPOTENCIA_CONFLICTO`.
- `Concepto` se acepta por compatibilidad con el contrato AFIP; la emisión del MVP calcula según el tipo y los ítems.
- Configuración: la fachada puede deshabilitarse por entorno; cuando está deshabilitada, la ruta responde `404`.

### Notas de crédito y débito


```json
{
  "tipoComprobante": 3,
  "puntoVenta": 1,
  "comprobanteOriginalId": "6ae24e50-1c3e-4f0a-9f3e-9d2f0b7a1c22",
  "receptor": {
    "documentoTipo": "CUIT",
    "documentoNumero": "30712345671",
    "razonSocial": "Cliente S.A.",
    "condicionIva": 1
  },
  "items": [
    { "descripcion": "Corrección de la factura 0001-00000042", "cantidad": 1, "precioUnitario": 1000, "alicuotaIva": 21 }
  ]
}
```

- `comprobanteOriginalId` es obligatorio en una nota y está prohibido en una factura (`400 VALIDACION`).
- El original tiene que ser una **factura** de la misma empresa, de la **misma letra** y en estado `Emitido`: una nota A solo ajusta una factura A, una B una factura B y una C una factura C. Una nota no puede ajustar otra nota.
- La nota tiene **número, CAE y vencimiento propios**, con su propia secuencia por punto de venta y tipo (la primera nota de cada tipo arranca en `1`). La factura original conserva su número y su CAE.
- Los importes se calculan igual que en las facturas de la misma letra: la nota A discrimina IVA y las B y C no.
- Cada factura lleva un **saldo ajustable** que arranca en su `importeTotal`. Una nota de crédito lo descuenta y **no puede superarlo** (`400 IMPORTE_EXCEDIDO`, sin consumir número ni CAE). Una nota de débito lo aumenta y no tiene límite.
- La nota y el ajuste del saldo se guardan en la misma transacción. Dos notas de crédito simultáneas sobre la misma factura no pueden dejar el saldo negativo: si la segunda ya no entra en el saldo, responde `IMPORTE_EXCEDIDO`.
- La respuesta `201` es el mismo `ComprobanteResponse` de las facturas, con los datos y el CAE de la nota. El saldo y la referencia al original son internos y no se devuelven.
- Cada CAE se verifica por separado en `GET /v1/comprobantes/verificar/{cae}`: la factura por el suyo y la nota por el propio.

### 5.2 Emisión manual desde el portal


Un usuario con rol `Administrador` o `Emisor` puede emitir sin escribir código desde `/comprobantes/nuevo`. El portal no calcula IVA ni genera número o CAE: arma el request y delega en la API.

Flujo:

1. `GET /v1/puntos-venta` con la cookie de empresa para llenar el combo de puntos de venta (solo se ofrecen los activos).
2. Al cambiar ítems, tipo o alícuota, el portal llama a `POST /v1/comprobantes/presupuesto` y muestra el neto, el IVA y el total que devuelve la API.
3. Al confirmar, `POST /v1/comprobantes` con la cookie de empresa y un header `Idempotency-Key` de 32 caracteres hexadecimales generado por el portal.
4. La pantalla de resultado muestra número, CAE, vencimiento y total, y enlaza a `GET /v1/comprobantes/{id}/pdf`.

Ejemplo de presupuesto (Factura A, 2 × 1000 al 21%):

```http
POST /v1/comprobantes/presupuesto
Cookie: <sesión de empresa>
Content-Type: application/json

{
  "tipoComprobante": 1,
  "puntoVenta": 1,
  "receptor": {
    "documentoTipo": "CUIT",
    "documentoNumero": "30712345671",
    "razonSocial": "Cliente S.A.",
    "condicionIva": 1
  },
  "items": [
    { "descripcion": "Servicio", "cantidad": 2, "precioUnitario": 1000, "alicuotaIva": 21 }
  ]
}
```

Respuesta `200`:

```json
{
  "importeNeto": 2000.00,
  "importeIva": 420.00,
  "importeTotal": 2420.00,
  "items": [
    { "descripcion": "Servicio", "cantidad": 2, "precioUnitario": 1000, "alicuotaIva": 21, "subtotal": 2000.00 }
  ]
}
```

- El presupuesto valida con las mismas reglas que la emisión y **no persiste**, no consume número, no genera CAE y no audita.
- La clave de idempotencia se conserva mientras el payload no cambie; si el usuario modifica ítems, receptor, tipo o punto de venta, el portal genera una clave nueva. Un reintento sin cambios devuelve el comprobante original (`200`).
- El botón «Emitir» se deshabilita mientras la petición está en vuelo para evitar el doble envío. Un rol `Consulta` recibe `403 SIN_PERMISOS` y el portal muestra «Acceso denegado».
- Los errores de validación se muestran junto al campo y conservan los datos cargados. Si la API responde un error de negocio, el portal muestra el `codigo` y el `mensaje` del `ErrorResponse`.

## 6. Idempotencia


Enviar un header `Idempotency-Key` único por operación:

```http
Idempotency-Key: pedido-2026-0001
```

El header es obligatorio en `POST /v1/comprobantes` y admite hasta 128 caracteres. La clave es única por empresa: la misma clave en otra empresa es una operación distinta, con su propio comprobante y su propio número.

Comportamiento por request:

| Situación | Respuesta |
| --- | --- |
| Primera emisión con una clave nueva | `201` con `ComprobanteResponse`. |
| Reintento con la misma clave y el mismo payload | `200` con el mismo `id`, `numero` y `cae`. No se renumera ni se genera otro CAE. |
| Misma clave con un payload distinto | `409 IDEMPOTENCIA_CONFLICTO`. El comprobante original no se modifica. |
| Falta el header o supera 128 caracteres | `400 VALIDACION`. |

- El payload se compara por su hash canónico: los importes se normalizan a 2 decimales, pero el orden de los ítems forma parte del payload. Cambiar el orden de los ítems con la misma clave produce `409 IDEMPOTENCIA_CONFLICTO`.
- La clave **no expira** y **no hay límite de intentos ni ventana temporal**: los reintentos con la misma clave y el mismo payload siguen devolviendo `200` con el comprobante original.
- Todas las respuestas, incluidas `401` y `403`, devuelven el header `Idempotency-Key` con la clave recibida.
- El header solo tiene efecto en `POST /v1/comprobantes`; no se usa para notas, pagos ni consultas.


## 7. Consultar comprobantes


Los tres endpoints aceptan **API key** (`X-Api-Key`) para integraciones o la **cookie de sesión de empresa** del portal, con rol `Administrador`, `Emisor` o `Consulta`. La empresa sale siempre de la credencial: nunca se envía en el body ni en el query string. Un comprobante de otra empresa responde `404`, sin revelar que existe.

### 7.1 Listado

```http
GET /v1/comprobantes?page=1&pageSize=20&desde=2026-10-01&hasta=2026-10-31&tipo=11&estado=1&puntoVenta=1
X-Api-Key: fak_<prefijo>_<secreto>
```

| Parámetro | Obligatorio | Regla |
| --- | --- | --- |
| `page` | No | Entero `>= 1`. Default `1`. |
| `pageSize` | No | Entero entre `1` y `100`. Default `20`. Un valor fuera de rango responde `400 VALIDACION`; no se recorta en silencio. |
| `desde` | No | Fecha `YYYY-MM-DD` o ISO 8601. Se interpreta en UTC e incluye desde las `00:00:00Z`. |
| `hasta` | No | Fecha `YYYY-MM-DD` o ISO 8601. Se interpreta en UTC e incluye el día completo: `hasta=2026-10-31` alcanza lo emitido el 31/10 a cualquier hora. |
| `tipo` | No | Código de `TipoComprobante`: `1`, `2`, `3`, `6`, `7`, `8`, `11`, `12`, `13`. Otro valor responde `400 VALIDACION`. |
| `estado` | No | Código de `EstadoComprobante`: `0`, `1`, `2`, `3`. Otro valor responde `400 VALIDACION`. |
| `puntoVenta` | No | Entero `>= 1`. |
| `receptor` | No | Documento del receptor. Se quitan guiones y espacios y se compara exacto. |

Los filtros se combinan con AND. Si `desde` es posterior a `hasta`, responde `400 VALIDACION`. El orden es `FechaEmision` descendente, `Numero` descendente e `Id` ascendente como desempate estable.

Respuesta `200`:

```json
{
  "items": [
    {
      "id": "6ae24e50-1c3e-4f0a-9f3e-9d2f0b7a1c22",
      "tipoComprobante": 11,
      "tipoComprobanteDescripcion": "Factura C",
      "puntoVenta": 1,
      "numero": 42,
      "fechaEmision": "2026-10-30T15:00:00Z",
      "cae": "12345678901234",
      "receptorRazonSocial": "Consumidor Final",
      "receptorDocumentoNumero": "0",
      "importeTotal": 10000.00,
      "moneda": "ARS",
      "estado": "Emitido"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "total": 1,
  "totalPages": 1
}
```

`total` es la cantidad de comprobantes que cumplen el filtro y `totalPages` es `ceil(total / pageSize)`. Una `page` mayor que `totalPages` devuelve `items: []` con los metadatos correctos; no es un error.

### 7.2 Detalle

```http
GET /v1/comprobantes/6ae24e50-1c3e-4f0a-9f3e-9d2f0b7a1c22
X-Api-Key: fak_<prefijo>_<secreto>
```

Respuesta `200`:

```json
{
  "id": "6ae24e50-1c3e-4f0a-9f3e-9d2f0b7a1c22",
  "tipoComprobante": 11,
  "tipoComprobanteDescripcion": "Factura C",
  "puntoVenta": 1,
  "numero": 42,
  "fechaEmision": "2026-10-30T15:00:00Z",
  "cae": "12345678901234",
  "caeVencimiento": "2026-11-09T15:00:00Z",
  "emisorCuit": "20123456786",
  "emisorRazonSocial": "Empresa Demo S.A.",
  "receptor": {
    "documentoTipo": "99",
    "documentoNumero": "0",
    "razonSocial": "Consumidor Final",
    "condicionIva": 5
  },
  "importeNeto": 10000.00,
  "importeIva": 0,
  "importeTotal": 10000.00,
  "moneda": "ARS",
  "estado": "Emitido",
  "observaciones": "Venta de prueba",
  "items": [
    {
      "descripcion": "Curso de programación",
      "cantidad": 1,
      "precioUnitario": 10000.00,
      "alicuotaIva": 0,
      "subtotal": 10000.00
    }
  ]
}
```

- Un id inexistente o de otra empresa responde `404 COMPROBANTE_NO_ENCONTRADO`.
- Un id que no es `Guid` responde `400 VALIDACION` y no consulta la base.
- La respuesta no expone el identificador interno de la empresa, la clave de idempotencia ni hashes.

### 7.3 Último autorizado

```http
GET /v1/comprobantes/ultimo-autorizado?tipo=11&puntoVenta=1
X-Api-Key: fak_<prefijo>_<secreto>
```

Respuesta `200`:

```json
{ "tipoComprobante": 11, "puntoVenta": 1, "numero": 42 }
```

- El parámetro `tipo` es **obligatorio** y se llama `tipo`, no `tipoComprobante`. Si falta o no es válido, responde `400 VALIDACION`.
- `puntoVenta` es opcional. Si se omite, la respuesta agrega el máximo por tipo y devuelve `"puntoVenta": null`.
- `numero` sale de la colección `secuencias` de `(empresa, punto de venta, tipo)`, no de recorrer `comprobantes`.
- Si no hay secuencia para esa combinación, responde `200` con `numero: 0`; no es un `404`.

### 7.4 Descarga del PDF


```http
GET /v1/comprobantes/6ae24e50-1c3e-4f0a-9f3e-9d2f0b7a1c22/pdf
X-Api-Key: fak_<prefijo>_<secreto>
```

Acepta **API key** (`X-Api-Key`) o la **cookie de sesión de empresa** con rol `Administrador`, `Emisor` o `Consulta`. La empresa sale siempre de la credencial y el comprobante se busca por `(Id, empresa)`: un comprobante de otra empresa responde `404 COMPROBANTE_NO_ENCONTRADO`, igual que uno inexistente, sin revelar que existe.

Respuesta `200`:

| Header | Valor |
| --- | --- |
| `Content-Type` | `application/pdf` |
| `Content-Disposition` | `inline; filename="{PuntoVenta:D4}-{Numero:D8}.pdf"` |

- El nombre del archivo solo contiene dígitos y guiones, por ejemplo `0001-00000001.pdf` para el punto de venta 1 y el número 1, o `0012-00000345.pdf` para el punto de venta 12 y el número 345.
- `inline` hace que el navegador abra el PDF en una pestaña en lugar de descargarlo; el portal lo abre en una pestaña nueva.
- El PDF incluye emisor, receptor, tipo de comprobante, punto de venta, número, fecha de emisión, CAE, vencimiento, ítems e importes.
- Incluye la leyenda literal «Comprobante simulado sin validez fiscal».
- El QR apunta a `{Portal:BaseUrl}/verificar?cae=<CAE>`; con la configuración local, `http://localhost:5116/verificar?cae=12345678901234`.
- Generar o regenerar el PDF **no altera el comprobante**: no inserta ni actualiza documentos, no consume números ni CAE y no cambia los datos fiscales. El PDF es una proyección del comprobante ya persistido.

Errores:

| Código | HTTP | Cuándo |
| --- | --- | --- |
| `VALIDACION` | `400` | El id de la ruta no es un `Guid`. |
| `API_KEY_AUSENTE` | `401` | No se envió el header `X-Api-Key` ni la cookie de empresa. |
| `API_KEY_INVALIDA` | `401` | La API key no existe, está revocada o el hash no coincide. |
| `CREDENCIALES_INVALIDAS` | `401` | La cookie de empresa no es válida o la sesión venció. |
| `SIN_PERMISOS` | `403` | El rol del principal no permite consultar comprobantes. |
| `COMPROBANTE_NO_ENCONTRADO` | `404` | El comprobante no existe o pertenece a otra empresa. |
| `ERROR_INTERNO` | `500` | Falló la composición del PDF; el detalle queda solo en el log. |

Los errores se devuelven como `ErrorResponse` JSON (`codigo` y `mensaje`), igual que en el resto de la API. Un fallo de generación nunca devuelve un PDF parcial ni un `200` con cuerpo vacío.

El contrato completo está en el contrato de la API.

### 7.5 Consultar por SOAP


Las tres operaciones usan SOAP 1.1 y el namespace `http://ar.gov.afip.dif.FEV1Service/`. La API key viaja en `X-Api-Key` o en `Auth/Token` dentro del sobre. La empresa sale siempre de la API key: nunca del cuerpo.

**Último autorizado.** Devuelve `UltimoNro = 0` cuando no hay comprobantes emitidos para el punto de venta y el tipo.

```http
POST /soap
X-Api-Key: fak_<prefijo>_<secreto>
SOAPAction: http://ar.gov.afip.dif.FEV1Service/FECompUltimoAutorizado
Content-Type: text/xml; charset=utf-8
```

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
    <FECompUltimoAutorizado xmlns="http://ar.gov.afip.dif.FEV1Service/">
      <request>
        <PtoVta>1</PtoVta>
        <TipoComprobante>11</TipoComprobante>
      </request>
    </FECompUltimoAutorizado>
  </soap:Body>
</soap:Envelope>
```

Respuesta `200` con `Resultado = "A"`, `UltimoNro = 42`, `Cae`, `FechaEmision` y `FchVto` (`yyyyMMdd`). Si no hay emitidos, `UltimoNro = 0` y los campos opcionales quedan vacíos.

**Consultar comprobante.** Un número de otra empresa responde `Resultado = "R"` con `COMPROBANTE_NO_ENCONTRADO` y sin datos fiscales, igual que un número inexistente.

```http
SOAPAction: http://ar.gov.afip.dif.FEV1Service/FECompConsultar
```

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
    <FECompConsultar xmlns="http://ar.gov.afip.dif.FEV1Service/">
      <request>
        <PtoVta>1</PtoVta>
        <TipoComprobante>11</TipoComprobante>
        <Numero>42</Numero>
      </request>
    </FECompConsultar>
  </soap:Body>
</soap:Envelope>
```

La respuesta `200` replica los campos del detalle de el comprobante: `Cuit`, `PtoVta`, `TipoComprobante`, `Numero`, `FechaEmision`, `Cae`, `FchVto`, `DocTipo`, `DocNro`, `CondicionIVA`, `Moneda`, `ImporteNeto`, `ImporteIva`, `ImporteTotal`, `Estado` y `Observaciones`.

**`FEDummy`.** Exige API key y devuelve `Estado = "OK"` y `ServerTime` en ISO 8601 UTC. No consulta la base de datos.

```http
SOAPAction: http://ar.gov.afip.dif.FEV1Service/FEDummy
```


### 7.6 Dashboard


```http
GET /v1/comprobantes/dashboard?mes=2026-10
X-Api-Key: fak_<prefijo>_<secreto>
```

Acepta **API key** (`X-Api-Key`) o la **cookie de sesión de empresa** con rol `Administrador`, `Emisor` o `Consulta`. La empresa sale siempre de la credencial: nunca se envía en el body ni en el query string.

| Parámetro | Obligatorio | Regla |
| --- | --- | --- |
| `mes` | No | Formato `YYYY-MM`. Default: mes actual en UTC. Otro formato responde `400 VALIDACION`. |

El periodo es `[desde, hasta)` en UTC: `mes=2026-10` va del `2026-10-01T00:00:00Z` al `2026-11-01T00:00:00Z`. `cantidadComprobantes` e `importeTotal` cuentan **solo** comprobantes en estado `Emitido`; `ultimoCae` es el CAE del comprobante emitido más reciente (`null` si no hay) y `ultimoNumeroAutorizado` es el máximo global de `UltimoNumero` de las secuencias de la empresa.

Respuesta `200` (`DashboardResponse`):

```json
{
  "periodo": { "desde": "2026-10-01T00:00:00Z", "hasta": "2026-11-01T00:00:00Z" },
  "cantidadComprobantes": 12,
  "importeTotal": 150000.00,
  "ultimoCae": "12345678901234",
  "ultimoNumeroAutorizado": 42,
  "ultimosComprobantes": [
    {
      "id": "6ae24e50-1c3e-4f0a-9f3e-9d2f0b7a1c22",
      "tipoComprobante": 11,
      "tipoComprobanteDescripcion": "Factura C",
      "puntoVenta": 1,
      "numero": 42,
      "fechaEmision": "2026-10-25T14:30:00Z",
      "cae": "12345678901234",
      "receptorRazonSocial": "Cliente Final",
      "receptorDocumentoNumero": "20345678901",
      "importeTotal": 121000.00,
      "moneda": "ARS",
      "estado": "Emitido"
    }
  ]
}
```

`ultimosComprobantes` son los cinco más recientes por `FechaEmision` descendente, con la misma forma de `ComprobanteResumenResponse`.

Errores:

| Código | HTTP | Cuándo |
| --- | --- | --- |
| `VALIDACION` | `400` | `mes` no tiene el formato `YYYY-MM`. |
| `API_KEY_AUSENTE` | `401` | No se envió el header `X-Api-Key` ni la cookie de empresa. |
| `API_KEY_INVALIDA` | `401` | La API key no existe, está revocada o el hash no coincide. |
| `CREDENCIALES_INVALIDAS` | `401` | La cookie de empresa no es válida o la sesión venció. |
| `SIN_PERMISOS` | `403` | El rol del principal no permite consultar comprobantes. |
| `ERROR_INTERNO` | `500` | Falla inesperada; el detalle queda solo en el log. |

El contrato completo está en el contrato de la API.

## 8. Verificar un comprobante


La verificación es pública y no requiere API key:

```http
GET /v1/comprobantes/verificar/12345678901234
```

Respuesta `200`:

```json
{
  "cae": "12345678901234",
  "cuitEmisor": "20123456786",
  "razonSocialEmisor": "Empresa de Prueba S.A.",
  "tipoComprobante": 1,
  "tipoComprobanteDescripcion": "Factura A",
  "puntoVenta": 1,
  "numero": 42,
  "fechaEmision": "2026-10-25T00:00:00Z",
  "importeTotal": 121000,
  "moneda": "ARS",
  "estado": "Emitido",
  "caeVencimiento": "2026-11-04T00:00:00Z"
}
```

Errores:

| Código | HTTP | Cuándo |
| --- | --- | --- |
| `CAE_INVALIDO` | `400` | El CAE no tiene 14 dígitos. |
| `COMPROBANTE_NO_ENCONTRADO` | `404` | El CAE tiene formato válido pero no existe. |

La respuesta no expone datos internos, secretos ni API keys.

## 9. Ejemplos


### curl

```bash
curl -X POST "http://localhost:5171/v1/comprobantes" \
  -H "X-Api-Key: fak_<prefijo>_<secreto>" \
  -H "Idempotency-Key: pedido-2026-0001" \
  -H "Content-Type: application/json" \
  -d '{
    "puntoVenta": 1,
    "tipoComprobante": 11,
    "receptor": {
      "documentoTipo": "99",
      "documentoNumero": "0",
      "razonSocial": "Consumidor Final",
      "condicionIva": 5
    },
    "items": [
      {
        "descripcion": "Curso de programación",
        "cantidad": 1,
        "precioUnitario": 10000,
        "alicuotaIva": 21
      }
    ]
  }'

curl -X GET "http://localhost:5171/v1/comprobantes?page=1&pageSize=20" \
  -H "X-Api-Key: fak_<prefijo>_<secreto>"

curl -X GET "http://localhost:5171/v1/comprobantes/verificar/12345678901234"
```

### JavaScript

```javascript
const emitirResponse = await fetch("http://localhost:5171/v1/comprobantes", {
  method: "POST",
  headers: {
    "Content-Type": "application/json",
    "X-Api-Key": process.env.FACTURAR_API_KEY,
    "Idempotency-Key": crypto.randomUUID()
  },
  body: JSON.stringify({
    puntoVenta: 1,
    tipoComprobante: 11,
    receptor: {
      documentoTipo: "99",
      documentoNumero: "0",
      razonSocial: "Consumidor Final",
      condicionIva: 5
    },
    items: [
      {
        descripcion: "Curso de programación",
        "cantidad": 1,
        "precioUnitario": 10000,
        "alicuotaIva": 21
      }
    ]
  })
});

const comprobante = await emitirResponse.json();

const listadoResponse = await fetch("http://localhost:5171/v1/comprobantes?page=1&pageSize=20", {
  headers: { "X-Api-Key": process.env.FACTURAR_API_KEY }
});
const listado = await listadoResponse.json();

const verificacionResponse = await fetch(`http://localhost:5171/v1/comprobantes/verificar/${comprobante.cae}`);
const verificacion = await verificacionResponse.json();
```

### C#

```csharp
using var client = new HttpClient();
client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());

var request = new
{
    puntoVenta = 1,
    tipoComprobante = 11,
    receptor = new
    {
        documentoTipo = "99",
        documentoNumero = "0",
        razonSocial = "Consumidor Final",
        condicionIva = 5
    },
    items = new[]
    {
        new
        {
            descripcion = "Curso de programación",
            cantidad = 1,
            precioUnitario = 10000,
            alicuotaIva = 21
        }
    }
};

var response = await client.PostAsJsonAsync(
    "http://localhost:5171/v1/comprobantes",
    request);

response.EnsureSuccessStatusCode();
var comprobante = await response.Content.ReadFromJsonAsync<JsonElement>();

var listado = await client.GetFromJsonAsync<JsonElement>(
    "http://localhost:5171/v1/comprobantes?page=1&pageSize=20");

var cae = comprobante.GetProperty("cae").GetString();
var verificacion = await client.GetFromJsonAsync<JsonElement>(
    $"http://localhost:5171/v1/comprobantes/verificar/{cae}");
```

## 10. Errores

Formato general:

```json
{
  "codigo": "VALIDACION",
  "mensaje": "El CUIT no es válido."
}
```

El cuerpo es el DTO `ErrorResponse` con los campos `codigo` y `mensaje`. No hay un campo `detalles`: los errores de validación no enumeran los campos que fallaron.

El catálogo cerrado de códigos es el de la tabla de este manual y coincide con el contrato de la API. Reglas generales:

- El `mensaje` es para humanos y puede cambiar sin romper clientes.
- El `codigo` no cambia.
- Nunca se devuelve un `500` por datos inválidos del cliente: es un `400` con código específico.

## 11. Seguridad

- Usar HTTPS en producción.
- Guardar la API key en un secret manager o variable de entorno del backend.
- No incluir la API key en código frontend, repositorios, logs ni capturas.
- No compartir la API key por email o chat.
- Rotar la key si se sospecha una filtración.
- Usar una key por integración y ambiente.
- Validar el CAE en la verificación pública antes de confiar en un comprobante.
- Recordar que el comprobante es simulado y no tiene validez fiscal.

## 12. Preguntas frecuentes

**¿Qué puedo consumir hoy?**
La emisión de facturas A, B y C y de notas de crédito y débito (`POST /v1/comprobantes`, con API key o sesión de empresa e `Idempotency-Key`), la emisión por SOAP (`POST /soap`, con `FECAESolicitar` y `FECAEGenerar`), las consultas de comprobantes (`GET /v1/comprobantes`, `GET /v1/comprobantes/{id}` y `GET /v1/comprobantes/ultimo-autorizado`), la descarga del PDF (`GET /v1/comprobantes/{id}/pdf`), la verificación pública por CAE (`GET /v1/comprobantes/verificar/{cae}`), la gestión de API keys (`/v1/api-keys`) y la emisión manual desde el portal (`POST /v1/comprobantes/presupuesto`, `GET /v1/puntos-venta` y `POST /v1/comprobantes` con sesión de empresa).

**¿Puedo registrarme solo con el CUIT?**
No. El CUIT es público. La cuenta se activa con una invitación enviada al email del solicitante y aprobada por el equipo de FacturAR.

**¿Qué pasa si mi invitación vence?**
Solicitar el reenvío al equipo de FacturAR. La invitación anterior se revoca y se genera una nueva.

**¿Puedo recuperar una API key?**
No. Solo se muestra una vez. Si se pierde, se regenera o se crea otra.

**¿La API key sirve para varias empresas?**
No. Cada key pertenece a una única empresa.

**¿Puedo usar la API desde el navegador?**
No se recomienda. La API key quedaría expuesta. Usar un backend propio.

**¿El CAE es real?**
No. Es un CAE simulado de 14 dígitos con vencimiento configurable (la configuración de vencimiento, default 10 días). No tiene validez fiscal.
