# Historias fuera de alcance

Esta carpeta contiene historias de usuario que **no forman parte del proyecto FacturAR en su estado actual**. No están en el sprint, no se implementan y no cuentan para la condición de salida.

Viven acá como registro de functionality alternativa, para reevaluarlas si el proyecto crece.

## Por qué están fuera de alcance

FacturAR es un **simulador** de facturación electrónica argentina. Su objetivo es que un equipo externo se integre de punta a punta (alta de empresa → API key → emisión → consultas → verificación) sinInteractuar con AFIP/ARCA real.

Las historias de esta carpeta apuntan al **escalado hacia producción real**:

| Historia | Qué agrega | Por qué no entra hoy |
| --- | --- | --- |
| [US-22](US-22-mvc-anulacion.md) | Vista MVC para anular comprobantes con motivo, conservando trazabilidad fiscal | Anular un documento con validez fiscal solo tiene sentido contra un organismo real. En el simulador el comprobante se puede volver a emitir |
| [US-23](US-23-webhooks.md) | Suscripción a eventos con entregas HTTP firmadas (HMAC), reintentos e historial | Es integración en tiempo real con sistemas de un cliente real. El MVP se integra con REST, SOAP y portal |
| [US-24](US-24-afip-real.md) | `AfipRealGateway` con WSAA y WSFEv1, certificado digital y traducción de errores de ARCA | Es exactamente lo contrario del objetivo del proyecto: el proyecto **es** el simulador. Requiere credenciales de ARCA |
| [US-25](US-25-grpc-graphql.md) | Segunda interfaz tipada (gRPC o GraphQL) para emitir y consultar | Duplica superficie de mantenimiento sin agregar valor al flujo del MVP, que ya se cubre con REST |

## Reglas

1. No implementar estas historias sin pedido explícito del usuario.
2. No contarlas para la condición de salida del sprint ni para el avance del MVP.
3. No agregarlas al índice de [`docs/historias-usuario/README.md`](../historias-usuario/README.md) mientras sigan fuera de alcance.
4. Si alguna se decide implementar, se mueve de vuelta a `docs/historias-usuario/` y se actualizan sus dependencias.
5. Estas historias dependen de funcionalidades del MVP (US-04, US-05, US-08, US-09, US-15, US-17). Si se activan, esas dependencias tienen que estar cerradas primero.

## Nota sobre US-26

No existe una historia US-26. El número quedó reservado y aparece citado en US-20 y US-31 como "stretch de Kubernetes", pero esa decisión de infraestructura ya está cerrada: ver [`docs/plan-implementacion.md`](../plan-implementacion.md) §2 — la plataforma objetivo es Azure App Service con contenedores y **no se usa Kubernetes**.
