# Manual de integración

## Qué es FacturAR

FacturAR es un simulador de facturación electrónica argentina. Permite solicitar el alta de una empresa, activar una cuenta, crear API keys, emitir comprobantes con CAE simulado y verificar un comprobante por su CAE.

> Importante: el CAE es simulado y los comprobantes no tienen validez fiscal. No se realizan llamadas reales a AFIP/ARCA.

## URLs

| Entorno | API | Portal |
| --- | --- | --- |
| Local | `http://localhost:5171` | `http://localhost:5116` |
| Docker | `http://localhost:8080` | `http://localhost:8081` |

OpenAPI en Development: `GET /openapi/v1.json`.

Salud: `GET /health`. Devuelve `200` si la API y MongoDB responden; `503` si MongoDB no está disponible.

## 1. Solicitar acceso

1. Ingresar al portal y abrir la solicitud de alta de empresa.
2. Completar razón social, CUIT, condición frente al IVA, datos del solicitante, email, teléfono y domicilio.
3. Aceptar los términos y enviar la solicitud.
4. Recibir el email de confirmación.

La solicitud queda pendiente de revisión. No se crea la empresa ni la cuenta en ese momento.

Un administrador global de FacturAR revisa la información, puede corregirla y aprueba o rechaza la solicitud. Si se aprueba, se crea la empresa, el punto de venta `1 Casa Central` y una invitación para el email del solicitante.

## 2. Activar la cuenta

1. Abrir el link del email de invitación.
2. Verificar la razón social y el email.
3. Definir una contraseña segura.
4. Confirmar la activación.
5. Iniciar sesión con el email y la contraseña.

La invitación vence a las 48 horas, es de un solo uso y solo es válida para el email destinatario. Si vence o se pierde, el administrador global puede reenviarla.

Política de contraseña:

- Mínimo 10 caracteres.
- Al menos una letra y un número.
- No se aceptan contraseñas triviales o iguales al email.
- La contraseña nunca se devuelve por la API.

## 3. Crear una API key

1. Iniciar sesión en el portal como administrador de la empresa.
2. Abrir la sección API keys.
3. Crear una key con un nombre descriptivo, por ejemplo `Ecommerce producción`.
4. Copiar el valor completo en ese momento.

El valor completo se muestra una sola vez. Después solo se ve el prefijo. Si se pierde, se regenera o se crea otra key.

Formato de referencia:

```text
fak_7f3a9c2e_<secreto>
```

- `fak` identifica una key de FacturAR.
- `7f3a9c2e` es el prefijo visible.
- `<secreto>` es la parte aleatoria que no se puede recuperar.

La key pertenece a una única empresa. No se comparte entre empresas ni se envía por email.

## 4. Autenticar requests

Enviar la API key en el header `X-Api-Key`:

```http
GET /v1/comprobantes/9 HTTP/1.1
Host: localhost:5171
X-Api-Key: fak_7f3a9c2e_<secreto>
Accept: application/json
```

Reglas:

- Sin key: `401 API_KEY_REQUERIDA`.
- Key inválida o revocada: `401 API_KEY_INVALIDA`.
- La key resuelve la empresa; nunca se envía `contribuyenteId` en el body.
- La verificación pública por CAE no requiere API key.

La API actualiza `UltimoUso` en cada autenticación exitosa.

## 5. Emitir una factura

> Este contrato se habilita con US-05. Hasta que la emisión esté implementada, el endpoint responde `404`.

```http
POST /v1/comprobantes HTTP/1.1
Host: localhost:5171
X-Api-Key: fak_7f3a9c2e_<secreto>
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
  "importeIva": 2100,
  "importeTotal": 12100,
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
- `importeIva = subtotal × alicuotaIva / 100`.
- `importeTotal = importeNeto + importeIva`.
- En Factura A el IVA se discrimina.
- En Factura B el IVA se incluye y no se discrimina.
- En Factura C no se discrimina IVA.

La emisión asigna número y CAE dentro de una transacción. El CAE tiene 14 dígitos y vence a los 10 días.

## 6. Idempotencia

Enviar un header `Idempotency-Key` único por operación:

```http
Idempotency-Key: pedido-2026-0001
```

Si el mismo request se repite con la misma key, la API devuelve el comprobante original sin volver a numerar ni generar un CAE nuevo. La clave es única por empresa.

Usar una key distinta para cada operación real. No reutilizar la key de un pedido exitoso para otro pedido.

## 7. Consultar comprobantes

> Este contrato se habilita con US-09.

```http
GET /v1/comprobantes?desde=2026-10-01&hasta=2026-10-31&page=1&pageSize=20
X-Api-Key: fak_7f3a9c2e_<secreto>
```

La respuesta incluye los comprobantes de la empresa autenticada, con paginación. Nunca se pueden consultar comprobantes de otra empresa.

Último número autorizado:

```http
GET /v1/comprobantes/ultimo-autorizado?puntoVenta=1&tipoComprobante=11
X-Api-Key: fak_7f3a9c2e_<secreto>
```

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
  -H "X-Api-Key: fak_7f3a9c2e_<secreto>" \
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
  "mensaje": "El CUIT no es válido.",
  "detalles": {
    "cuit": ["Debe tener 11 dígitos y dígito verificador válido."]
  }
}
```

| Código | HTTP | Significado |
| --- | --- | --- |
| `API_KEY_REQUERIDA` | `401` | Falta el header `X-Api-Key`. |
| `API_KEY_INVALIDA` | `401` | La key no existe, está revocada o tiene formato inválido. |
| `CREDENCIALES_INVALIDAS` | `401` | Email o contraseña incorrectos. |
| `NO_AUTORIZADO` | `401` | No hay sesión de portal válida. |
| `SIN_PERMISO` | `403` | El rol no permite la acción. |
| `VALIDACION` | `400` | Datos inválidos. |
| `PASSWORD_DEBIL` | `400` | La contraseña no cumple la política. |
| `NO_ENCONTRADO` | `404` | El recurso no existe o no pertenece a la empresa. |
| `INVITACION_VENCIDA` | `410` | El token de invitación expiró. |
| `CONFLICTO` | `409` | Duplicado, estado incompatible o idempotencia en conflicto. |
| `SOLICITUD_PENDIENTE` | `409` | Ya existe una solicitud pendiente para el CUIT o email. |
| `INVITACION_USADA` | `409` | El token de invitación ya fue utilizado. |
| `RATE_LIMIT` | `429` | Demasiados intentos. |
| `ERROR_INTERNO` | `500` | Error inesperado. |

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
