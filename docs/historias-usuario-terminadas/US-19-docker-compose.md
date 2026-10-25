# US-19 — Docker y Docker Compose

**Épica:** Infraestructura  
**Prioridad:** Alta  
**Estimación:** 2 días  
**Requisito:** RF-21

## Historia

Como desarrollador, quiero levantar API y portal con Docker Compose para reproducir el entorno con un solo comando.

## Criterios de aceptación

- [x] La API y el portal tienen Dockerfiles multi-stage.
- [x] Las imágenes finales usan el runtime de ASP.NET y no el SDK.
- [x] Los contenedores corren con usuario no root.
- [x] Docker Compose de desarrollo construye ambas imágenes y expone sus puertos.
- [x] Docker Compose de producción usa imágenes de Docker Hub.
- [x] La connection string se pasa por variable de entorno.
- [x] El portal se comunica con la API por la red interna de Compose.
- [x] El arranque no requiere MongoDB dentro de Compose.

## Tareas técnicas

- [x] Crear `facturAR_backend/Dockerfile`.
- [x] Crear `facturAR_frontend/Dockerfile`.
- [x] Crear `docker-compose.yml`.
- [x] Crear `docker-compose.prod.yml`.
- [x] Crear `.env.example` y `.env.production.example`.
- [x] Probar los builds en Docker Desktop.
- [x] Documentar el uso en el README.

## Definition of Done

- [x] `docker compose up --build` levanta API y portal.
- [x] El health check de la API responde según la disponibilidad de MongoDB.
