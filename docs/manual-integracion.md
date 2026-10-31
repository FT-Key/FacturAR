# Manual de integración

## Estado de la API

> **Diseño objetivo del MVP.** Este manual describe el contrato que la API de FacturAR va a exponer. **Casi nada de eso existe todavía en el código**: las historias US-XX que lo habilitan están pendientes o en curso. Lo único que responde hoy es lo que figura en "Endpoints disponibles hoy".

Qué describe este documento: cómo un tercero pide acceso, activa su cuenta, crea una API key, emite comprobantes, los consulta, usa idempotencia y verifica un comprobante por su CAE.

- **Contrato normativo:** [`docs/contratos-api.md`](contratos-api.md) fija los códigos de error, los roles, la autenticación y el contrato de cada endpoint. Si el manual y el contrato se contradicen, manda el contrato.
- **Estado real de cada historia:** [`docs/historias-usuario/README.md`](historias-usuario/README.md).

### Endpoints disponibles hoy

Base URL local: `http://localhost:5171`. En Docker: `http://localhost:8080`.

| Método | Ruta | Qué hace |
| --- | --- | --- |
| `GET` | `/` | Nombre del servicio, versión y commit. |
| `GET` | `/health` | Readiness: verifica MongoDB. `200` si responde, `503` si no. |
| `GET` | `/health/live` | Liveness: no consulta MongoDB. Siempre `200`. |
| `GET` | `/v1/comprobantes/verificar/{cae}` | Verificación pública de un comprobante por CAE. No requiere autenticación. |

Todo lo demás que aparece en este manual está **especificado pero no implementado**: esas rutas responden `404` hasta que su historia se complete.

### Endpoints del MVP y su estado

| Método | Ruta | Autenticación | Estado |
| --- | --- | --- | --- |
| `GET` | `/` | — | disponible hoy |
| `GET` | `/health` | — | disponible hoy |
| `GET` | `/health/live` | — | disponible hoy |
| `GET` | `/v1/comprobantes/verificar/{cae}` | ninguna | disponible hoy |
| `POST` | `/v1/solicitudes-alta` | público, con rate limit | especificado, pendiente de implementar (US-01) |
| `GET` | `/v1/admin/solicitudes` | cookie de consola global | especificado, pendiente de implementar (US-27) |
| `POST` | `/v1/admin/solicitudes/{id}/aprobar` | cookie de consola global | especificado, pendiente de implementar (US-27) |
| `POST` | `/v1/admin/solicitudes/{id}/rechazar` | cookie de consola global | especificado, pendiente de implementar (US-27) |
| `POST` | `/v1/admin/solicitudes/{id}/reenviar-invitacion` | cookie de consola global | especificado, pendiente de implementar (US-27) |
| `GET` | `/v1/invitaciones/{token}` | público, con rate limit | implementado (US-28) |
| `POST` | `/v1/invitaciones/{token}/aceptar` | público, con rate limit | implementado (US-28) |
| `GET` | `/v1/api-keys` | cookie de empresa | especificado, pendiente de implementar (US-02) |
| `POST` | `/v1/api-keys` | cookie de empresa | especificado, pendiente de implementar (US-02) |
| `DELETE` | `/v1/api-keys/{id}` | cookie de empresa | especificado, pendiente de implementar (US-02) |
| `POST` | `/v1/api-keys/{id}/regenerar` | cookie de empresa | especificado, pendiente de implementar (US-02) |
| `POST` | `/v1/comprobantes` | API key | especificado, pendiente de implementar (US-05) |
| `GET` | `/v1/comprobantes` | API key | especificado, pendiente de implementar (US-09) |
| `GET` | `/v1/comprobantes/{id}` | API key | especificado, pendiente de implementar (US-09) |
| `GET` | `/v1/comprobantes/ultimo-autorizado` | API key | especificado, pendiente de implementar (US-09) |
| `GET` | `/v1/comprobantes/{id}/pdf` | API key | especificado, pendiente de implementar (US-10) |

La API key viaja en el header `X-Api-Key`. La sesión del portal usa cookies con esquemas separados: `cookies.contribuyente` para empresas y `cookies.admin` para la consola global. Ver `docs/contratos-api.md` §4.

### Headers

| Header | Dónde | Regla |
| --- | --- | --- |
| `X-Api-Key` | Endpoints de negocio | Obligatorio. Nombre configurable con `ApiKeys__HeaderName` (default `X-Api-Key`). Formato `^fak_[0-9a-f]{12}_[A-Za-z0-9_-]{43}$`, máximo 200 caracteres. Si falta: `401 API_KEY_AUSENTE`. Si no existe, está revocada, el hash no coincide o la empresa está inactiva: `401 API_KEY_INVALIDA`. Si Mongo no responde: `503 MONGO_NO_DISPONIBLE`. |
| `Idempotency-Key` | `POST /v1/comprobantes` | Obligatorio. Máximo 128 caracteres. Alcance `(empresa, clave)`. La misma clave con otro payload: `409 IDEMPOTENCIA_CONFLICTO`. |

### Códigos de error

El catálogo es cerrado y vive en `docs/contratos-api.md` §2.1. Un cliente puede hacer switch sobre los códigos: son parte del contrato público.

| Código | HTTP | Cuándo |
| --- | --- | --- |
| `VALIDACION` | `400` | El cuerpo o un parámetro no cumple el contrato. Es el código genérico de validación. |
| `CUIT_INVALIDO` | `400` | El CUIT no supera la validación de dígito verificador. |
| `CAE_INVALIDO` | `400` | El CAE no tiene 14 dígitos válidos. |
| `PASSWORD_DEBIL` | `400` | La contraseña no cumple la política de contraseñas. |
| `EMISION_INVALIDA` | `400` | El comprobante a emitir no supera las reglas de negocio. |
| `TIPO_NOTA_INCOMPATIBLE` | `400` | Una nota de crédito o débito apunta a un comprobante de otro tipo base. |
| `IMPORTE_EXCEDIDO` | `400` | El importe de la nota supera el saldo ajustable del comprobante original. |
| `API_KEY_AUSENTE` | `401` | No se envió el header `X-Api-Key`. |
| `API_KEY_INVALIDA` | `401` | La API key no existe, está revocada o el hash no coincide. |
| `CREDENCIALES_INVALIDAS` | `401` | Email o contraseña incorrectos, o la cuenta está inactiva. |
| `SIN_PERMISOS` | `403` | El rol no alcanza para la operación. |
| `CUENTA_INACTIVA` | `403` | El contribuyente o el usuario está desactivado. |
| `EMPRESA_NO_ASOCIADA` | `403` | El usuario no tiene empresa activa asociada. |
| `SOLICITUD_NO_ENCONTRADA` | `404` | No existe la solicitud. |
| `INVITACION_NO_ENCONTRADA` | `404` | No existe la invitación. |
| `COMPROBANTE_NO_ENCONTRADO` | `404` | No existe un comprobante con el CAE o el id indicado. |
| `SOLICITUD_PENDIENTE` | `409` | Ya existe una solicitud en estado `Pendiente` para el mismo CUIT o email. |
| `CUIT_YA_REGISTRADO` | `409` | Al aprobar, el CUIT de la solicitud ya pertenece a otro contribuyente. |
| `SOLICITUD_YA_PROCESADA` | `409` | La solicitud ya fue aprobada o rechazada. |
| `INVITACION_USADA` | `409` | El token de invitación ya fue aceptado. |
| `IDEMPOTENCIA_CONFLICTO` | `409` | La misma `Idempotency-Key` llegó con un payload diferente. |
| `COMPROBANTE_ANULADO` | `409` | El comprobante está anulado y no admite la operación. |
| `PUNTO_VENTA_INACTIVO` | `409` | El punto de venta no está activo. |
| `INVITACION_VENCIDA` | `410` | El token de invitación superó las 48 horas. |
| `RATE_LIMIT` | `429` | Se superó el límite de peticiones del endpoint. |
| `EMAIL_NO_ENVIADO` | `502` | Falló el envío del email. La operación de negocio no se revierte. |
| `MONGO_NO_DISPONIBLE` | `503` | No hay conexión con MongoDB. |
| `ERROR_INTERNO` | `500` | Falla inesperada. El detalle queda en el log, nunca en la respuesta. |

### Qué historia implementa qué parte de este manual

| Historia | Qué parte del manual describe | Estado |
| --- | --- | --- |
| US-01 | §1 Solicitar acceso: formulario público de alta | Pendiente |
| US-02 | §3 Crear una API key | Pendiente |
| US-03 | §4 Autenticar requests con API key | Base implementada |
| US-04 | §2 Activar la cuenta: login de empresa y roles | Pendiente |
| US-05 | §5 Emitir una factura | Pendiente |
| US-06 | §6 Idempotencia | Pendiente |
| US-07 | Numeración correlativa del comprobante (reglas de §5) | Pendiente |
| US-09 | §7 Consultar comprobantes | Pendiente |
| US-10 | Descarga del PDF (`GET /v1/comprobantes/{id}/pdf`) | Pendiente |
| US-11 | §8 Verificar un comprobante por CAE | Base implementada |
| US-27 | §1 Revisión y aprobación del administrador global | Pendiente |
| US-28 | §2 Activar la cuenta: invitación por token | Completada |
| US-29 | Emails de onboarding de §1 y §2 | Pendiente |
| US-21 | Veracidad documental de este manual | Parcial |
| US-30 | Publicación del manual online en el portal | Pendiente |

## Qué es FacturAR

FacturAR es un simulador de facturación electrónica argentina. El diseño del MVP permite solicitar el alta de una empresa, activar una cuenta, crear API keys, emitir comprobantes con CAE simulado y verificar un comprobante por su CAE. De ese recorrido, hoy solo la verificación por CAE está implementada: ver [Estado de la API](#estado-de-la-api).

> Importante: el CAE es simulado y los comprobantes no tienen validez fiscal. No se realizan llamadas reales a AFIP/ARCA.

## URLs

| Entorno | API | Portal |
| --- | --- | --- |
| Local | `http://localhost:5171` | `http://localhost:5116` |
| Docker | `http://localhost:8080` | `http://localhost:8081` |

OpenAPI en Development: `GET /openapi/v1.json`.

Salud: `GET /health` (readiness, verifica MongoDB) y `GET /health/live` (liveness, no consulta MongoDB). El health check de Azure App Service apunta a `/health/live`.

## 1. Solicitar acceso

> **Diseño objetivo — US-01, US-27, US-29.** El formulario público de alta, la consola del administrador global y los emails de onboarding **todavía no están implementados**. Las rutas `POST /v1/solicitudes-alta` y `/v1/admin/solicitudes/**` responden `404` hasta que US-01, US-27 y US-29 se completen.

1. Ingresar al portal y abrir la solicitud de alta de empresa.
2. Completar razón social, CUIT, condición frente al IVA, datos del solicitante, email, teléfono y domicilio.
3. Aceptar los términos y enviar la solicitud.
4. Recibir el email de confirmación.

La solicitud queda pendiente de revisión. No se crea la empresa ni la cuenta en ese momento.

Un administrador global de FacturAR revisa la información, puede corregirla y aprueba o rechaza la solicitud. Si se aprueba, se crea la empresa, el punto de venta `1 Casa Central` y una invitación para el email del solicitante.

Errores del flujo: `400 VALIDACION` y `400 CUIT_INVALIDO` por datos inválidos, `409 SOLICITUD_PENDIENTE` si ya hay una solicitud pendiente con el mismo CUIT o email, `429 RATE_LIMIT` por exceso de peticiones y `404 SOLICITUD_NO_ENCONTRADA` / `409 SOLICITUD_YA_PROCESADA` / `409 CUIT_YA_REGISTRADO` en la consola global.

## 2. Activar la cuenta

> **Estado — US-28, US-04, US-29.** La activación por invitación (US-28) y el login de empresa (US-04) están implementados: `GET /v1/invitaciones/{token}` y `POST /v1/invitaciones/{token}/aceptar` ya responden. Los emails transaccionales (US-29) siguen parciales. Errores: `404 INVITACION_NO_ENCONTRADA`, `410 INVITACION_VENCIDA`, `409 INVITACION_USADA`, `403 CUENTA_INACTIVA`, `400 PASSWORD_DEBIL`, `400 VALIDACION` y `429 RATE_LIMIT`, con rate limit por IP y hora configurable en `RateLimit:Invitacion`.

1. Abrir el link del email de invitación.
2. Verificar la razón social y el email.
3. Definir una contraseña segura.
4. Confirmar la activación.
5. Iniciar sesión con el email y la contraseña.

La invitación vence a las 48 horas, es de un solo uso y solo es válida para el email destinatario. Si vence o se pierde, el administrador global puede reenviarla.

Política de contraseña:

- Mínimo 10 caracteres y máximo 128.
- Al menos una letra y un número.
- No se aceptan contraseñas triviales ni predecibles.
- La contraseña nunca se devuelve por la API.

Errores del flujo: `404 INVITACION_NO_ENCONTRADA`, `410 INVITACION_VENCIDA`, `409 INVITACION_USADA`, `400 PASSWORD_DEBIL` y `400 VALIDACION` si la confirmación no coincide.

## 3. Crear una API key

> **Diseño objetivo — US-02 y US-14.** La gestión de API keys **todavía no está implementada**. `GET|POST /v1/api-keys`, `DELETE /v1/api-keys/{id}` y `POST /v1/api-keys/{id}/regenerar` responden `404` hasta que US-02 se complete.

1. Iniciar sesión en el portal como administrador de la empresa.
2. Abrir la sección API keys.
3. Crear una key con un nombre descriptivo, por ejemplo `Ecommerce producción`.
4. Copiar el valor completo en ese momento.

El valor completo se muestra una sola vez. Después solo se ve el prefijo. Si se pierde, se regenera o se crea otra key.

Formato de referencia:

```text
fak_9f3a1c7b2e40_<secreto>
```

La expresión regular de validación es `^fak_[0-9a-f]{12}_[A-Za-z0-9_-]{43}$`.

- `fak` identifica una key de FacturAR.
- `9f3a1c7b2e40` son los 12 caracteres hexadecimales del prefijo visible.
- `<secreto>` son los 43 caracteres base64url de la parte aleatoria, que no se puede recuperar.

En la base se guarda el prefijo y el hash SHA-256 del valor completo, nunca el valor completo.

La key pertenece a una única empresa. No se comparte entre empresas ni se envía por email.

## 4. Autenticar requests

> **Base implementada — US-03.** El esquema `ApiKey`, la política `ApiKeyNegocio` y el registro de `UltimoUso` están implementados y probados. Los endpoints de negocio que los consumen llegan con US-05 y US-09: hasta entonces esas rutas responden `404` porque el endpoint todavía no existe.

Enviar la API key en el header `X-Api-Key`. El nombre del header es configurable con `ApiKeys__HeaderName` (default `X-Api-Key`):

```http
GET /v1/comprobantes HTTP/1.1
Host: localhost:5171
X-Api-Key: fak_9f3a1c7b2e40_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
Accept: application/json
```

Formato: `fak_` + 12 caracteres hexadecimales del prefijo + `_` + 43 caracteres base64url del secreto. La expresión regular es `^fak_[0-9a-f]{12}_[A-Za-z0-9_-]{43}$` y el valor admitido tiene como máximo 200 caracteres.

Reglas:

- Sin header, o con el header vacío: `401 API_KEY_AUSENTE`.
- Key mal formada, inexistente, revocada o de un contribuyente inactivo: `401 API_KEY_INVALIDA`. El mensaje es el mismo en todos los casos: la respuesta no revela si el prefijo existe.
- Si MongoDB no responde durante la autenticación: `503 MONGO_NO_DISPONIBLE`.
- La key resuelve la empresa. El tenant sale **solo** del claim `contribuyenteId` derivado de la key; nunca se envía `contribuyenteId` en el body ni en el query string y, si se envía, se ignora.
- La cookie del portal no autoriza la API de negocio, y la API key no autoriza los endpoints de cookie.
- La verificación pública por CAE (`GET /v1/comprobantes/verificar/{cae}`) permanece anónima y no requiere API key.

Ejemplo de error de autenticación:

```json
{
  "codigo": "API_KEY_AUSENTE",
  "mensaje": "Se requiere el header X-Api-Key."
}
```

La API actualiza `UltimoUso` en cada autenticación exitosa, con granularidad de `ApiKeys__IntervaloActualizacionUltimoUsoMinutos` (default 1 minuto). Si esa actualización falla, la respuesta de negocio no se bloquea ni se rompe; el fallo solo queda como advertencia en el log.

## 5. Emitir una factura

> **Diseño objetivo — US-05 y US-07.** La emisión **todavía no está implementada**. `POST /v1/comprobantes` responde `404` hasta que US-05 se complete.

```http
POST /v1/comprobantes HTTP/1.1
Host: localhost:5171
X-Api-Key: fak_9f3a1c7b2e40_<secreto>
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

Respuesta esperada `201`:

```json
{
  "id": "4a4f3f4e-7d7a-4f4e-9a5e-2f6b8c9d0e1f",
  "puntoVenta": 1,
  "tipoComprobante": 11,
  "numero": 1,
  "cae": "12345678901234",
  "caeVencimiento": "2026-11-04T00:00:00Z",
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

Errores del flujo: `400 VALIDACION`, `400 EMISION_INVALIDA`, `400 CUIT_INVALIDO`, `409 PUNTO_VENTA_INACTIVO` y `404 COMPROBANTE_NO_ENCONTRADO` si el punto de venta no existe o es de otra empresa.

## 6. Idempotencia

> **Diseño objetivo — US-06.** La idempotencia **todavía no está implementada**: viene con el endpoint de emisión de US-05.

Enviar un header `Idempotency-Key` único por operación:

```http
Idempotency-Key: pedido-2026-0001
```

El header es obligatorio en `POST /v1/comprobantes` y admite hasta 128 caracteres. La clave es única por empresa: la misma clave en otra empresa es una operación distinta.

Si el mismo request se repite con la misma key, la API devuelve el comprobante original sin volver a numerar ni generar un CAE nuevo. Si la misma clave llega con otro payload, responde `409 IDEMPOTENCIA_CONFLICTO`.

Usar una key distinta para cada operación real. No reutilizar la key de un pedido exitoso para otro pedido.

## 7. Consultar comprobantes

> **Diseño objetivo — US-09.** Las consultas **todavía no están implementadas**. `GET /v1/comprobantes`, `GET /v1/comprobantes/{id}` y `GET /v1/comprobantes/ultimo-autorizado` responden `404` hasta que US-09 se complete.

```http
GET /v1/comprobantes?desde=2026-10-01&hasta=2026-10-31&page=1&pageSize=20
X-Api-Key: fak_9f3a1c7b2e40_<secreto>
```

La respuesta incluye los comprobantes de la empresa autenticada, con paginación. Nunca se pueden consultar comprobantes de otra empresa.

Último número autorizado:

```http
GET /v1/comprobantes/ultimo-autorizado?tipo=11&puntoVenta=1
X-Api-Key: fak_9f3a1c7b2e40_<secreto>
```

## 8. Verificar un comprobante

> **Disponible hoy — US-11 (base implementada).** Este es el único endpoint de negocio que responde: no requiere API key.

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

> **Diseño objetivo — US-05.** Los ejemplos de emisión funcionan cuando exista `POST /v1/comprobantes`. Hoy no hay forma de emitir una factura desde la API.

### curl

```bash
curl -X POST "http://localhost:5171/v1/comprobantes" \
  -H "X-Api-Key: fak_9f3a1c7b2e40_<secreto>" \
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
```

### JavaScript

```javascript
const response = await fetch("http://localhost:5171/v1/comprobantes", {
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
        cantidad: 1,
        precioUnitario: 10000,
        alicuotaIva: 21
      }
    ]
  })
});

const comprobante = await response.json();
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

El catálogo cerrado de códigos está en [Estado de la API](#estado-de-la-api) y es el mismo de `docs/contratos-api.md` §2.1. Reglas generales:

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
Solo la verificación pública por CAE (`GET /v1/comprobantes/verificar/{cae}`) y los endpoints de salud. El resto del contrato está especificado en `docs/contratos-api.md` y pendiente de implementación.

**¿Puedo registrarme solo con el CUIT?**
No. El CUIT es público. La cuenta se activa con una invitación enviada al email del solicitante y aprobada por un administrador global.

**¿Qué pasa si mi invitación vence?**
Solicitar el reenvío al administrador global. La invitación anterior se revoca y se genera una nueva.

**¿Puedo recuperar una API key?**
No. Solo se muestra una vez. Si se pierde, se regenera o se crea otra.

**¿La API key sirve para varias empresas?**
No. Cada key pertenece a una única empresa.

**¿Puedo usar la API desde el navegador?**
No se recomienda. La API key quedaría expuesta. Usar un backend propio.

**¿El CAE es real?**
No. Es un CAE simulado de 14 dígitos con vencimiento a 10 días. No tiene validez fiscal.
