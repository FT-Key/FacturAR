# Requisitos no funcionales

## Contexto

FacturAR es un TFI desarrollado por 4-5 personas en 3 semanas. Los requisitos no funcionales priorizan una demo estable, segura y reproducible por sobre una operación de escala real.

## Requisitos

| ID | Categoría | Requisito | Criterio de verificación |
| --- | --- | --- | --- |
| RNF-01 | Rendimiento | La verificación pública por CAE debe responder en menos de 1 segundo en condiciones normales. | Prueba con 1.000 comprobantes e índice por CAE. |
| RNF-02 | Rendimiento | La emisión de un comprobante debe completarse en menos de 3 segundos, sin contar la generación del PDF. | Medición en entorno de staging. |
| RNF-03 | Disponibilidad | El servicio debe exponer `/health` y reiniciarse automáticamente en producción. | `restart: unless-stopped`, health check de App Service y reinicio automático. |
| RNF-04 | Seguridad | Las API keys se almacenan hasheadas; nunca en texto plano. | Revisión de la colección `apiKeys`. |
| RNF-05 | Seguridad | Las contraseñas se almacenan con un algoritmo de hash robusto. | Revisión del registro de usuario. |
| RNF-06 | Seguridad | Las connection strings y secretos se inyectan por variables de entorno. | `.env` fuera del repositorio y `.gitignore`. |
| RNF-07 | Seguridad | El frontend no tiene acceso directo a MongoDB. | Revisión de configuración y dependencias. |
| RNF-08 | Integridad | La numeración y el CAE son únicos; se garantizan con índices y transacciones. | Tests de concurrencia e índices únicos. |
| RNF-09 | Multiempresa | Toda consulta de datos de negocio se filtra por `ContribuyenteId`. | Revisión de repositorios y tests de aislamiento. |
| RNF-10 | Portabilidad | Backend y frontend se ejecutan en contenedores Linux. | `docker compose up --build`. |
| RNF-11 | Configurabilidad | Los entornos se configuran sin recompilar, mediante variables de entorno. | Cambio de connection string sin rebuild. |
| RNF-12 | Mantenibilidad | Separación en Domain, Application, Infrastructure, Contracts y Api. | Revisión de dependencias entre proyectos. |
| RNF-13 | Calidad | La solución compila sin advertencias y los tests unitarios pasan. | `dotnet build` y `dotnet test`. |
| RNF-14 | Observabilidad | La API registra requests, errores y estado de MongoDB. | Logs de ASP.NET Core y `/health`. |
| RNF-15 | Interoperabilidad | La API REST usa JSON y la API SOAP respeta el contrato de AFIP. | Pruebas con Postman/SoapUI. |
| RNF-16 | Usabilidad | El portal es responsive y muestra mensajes claros de éxito o error. | Revisión en desktop y mobile. |
| RNF-17 | Documentación | README, OpenAPI, requisitos, modelos, manual e historias están versionados. | Revisión del repositorio. |
| RNF-18 | Testing | Al menos los casos críticos de onboarding, CUIT, CAE, numeración, idempotencia y aislamiento tienen tests. | Cobertura de casos críticos. |
| RNF-19 | Datos | Fechas en UTC y montos con `decimal`/`Decimal128`. | Revisión del modelo y de la base. |
| RNF-20 | Restricciones | La solución debe poder desarrollarse y demostrarse con recursos gratuitos o de bajo costo. | Atlas M0, App Service B1 (elegido para la demo por Always On), Docker Hub y GitHub Actions free. |
| RNF-21 | Seguridad de onboarding | Los tokens de invitación se almacenan hasheados, son de un solo uso, vencen a las 48 horas y están asociados a un email. | Revisión de la colección `invitaciones` y tests de token vencido/usado. |
| RNF-22 | Confiabilidad de emails | Un fallo de email no debe perder la solicitud, la empresa ni la invitación; debe permitir reenvío. | Test de fallo de SMTP y aprobación exitosa. |
| RNF-23 | Protección de abuso | Los endpoints públicos de solicitud, activación y login tienen rate limit. | Test de `429` y configuración de límites. |
| RNF-24 | Auditoría | Las acciones de onboarding, autenticación, API keys y emisión quedan auditadas sin secretos. | Revisión de eventos y tests. |
| RNF-25 | Privacidad | La solicitud no revela si un CUIT ya existe y la activación no revela datos de otras empresas. | Tests de respuestas genéricas y aislamiento. |
| RNF-26 | Accesibilidad | El formulario de solicitud, la activación y el manual son navegables por teclado y tienen etiquetas. | Revisión manual y prueba con teclado. |

## Restricciones técnicas

- .NET 10 y C# 14.
- MongoDB como única base de datos.
- Docker para desarrollo y producción.
- GitHub Actions y Docker Hub con workflows separados de CI y CD.
- Sin Kubernetes, RabbitMQ ni SQL Server en el alcance obligatorio. La decisión de plataforma está fechada en [`docs/historias-usuario/US-26-kubernetes.md`](historias-usuario/US-26-kubernetes.md): el proyecto es académico, lo consumen 1 a 3 aplicaciones externas y Azure App Service cubre el auto-restart, el health check, el escalado y el rollback sin costo de orquestación.
- Sin proveedor de email obligatorio en desarrollo; SMTP es opcional en producción.

## Métricas de aceptación del TFI

- Todos los endpoints del alcance obligatorio responden según contrato.
- La demo permite solicitar alta, aprobar, activar cuenta, iniciar sesión, crear API key, emitir, listar, descargar y verificar.
- La API key protege los endpoints de negocio.
- La verificación pública funciona sin autenticación.
- El manual permite a un tercero integrar la API.
- El despliegue se reproduce con un comando de Docker Compose.
- La documentación permite a un tercero levantar el sistema.
- No hay secretos en el repositorio ni en la documentación.
