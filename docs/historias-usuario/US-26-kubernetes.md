# US-26 — Despliegue en Kubernetes

**Épica:** Infraestructura  
**Prioridad:** Baja (stretch)  
**Estimación:** 3 días  
**Requisito:** RNF-10  
**Depende de:** US-19, US-20  
**Estado:** Stretch

## Historia

Como equipo de operación, quiero desplegar FacturAR en Kubernetes para escalar y recuperar los servicios automáticamente sin cambiar la aplicación.

## Contexto

La aplicación ya se empaqueta en imágenes Docker. Kubernetes agrega manifiestos, probes, configuración y secretos. No forma parte del MVP.

## Criterios de aceptación

- [ ] Existen manifiestos o un chart para API y portal.
- [ ] Las imágenes se toman de Docker Hub por tag.
- [ ] La configuración se inyecta por ConfigMap y los secretos por Secret.
- [ ] La API tiene readiness y liveness probes contra `/health`.
- [ ] El portal tiene probes de disponibilidad.
- [ ] Se definen requests y limits de CPU y memoria.
- [ ] Se puede escalar la API horizontalmente.
- [ ] Hay un Ingress o Service documentado.
- [ ] El despliegue no requiere cambiar el código.
- [ ] Hay instrucciones de despliegue, rollback y diagnóstico.

## Escenarios Gherkin

```gherkin
Scenario: Despliegue inicial
  Given un clúster con kubectl configurado
  When se aplican los manifiestos
  Then la API y el portal quedan disponibles
  And los probes reportan salud
```

```gherkin
Scenario: Falla de MongoDB
  Given la API no puede conectarse a MongoDB
  When el probe de readiness se ejecuta
  Then la API no recibe tráfico
  And el pod se mantiene para diagnóstico
```

```gherkin
Scenario: Rollback
  Given un despliegue con una imagen defectuosa
  When se aplica el tag anterior
  Then el servicio vuelve a la versión estable
```

## Tareas técnicas

- [ ] Crear manifiestos o chart de API y portal.
- [ ] Configurar ConfigMap y Secret.
- [ ] Configurar probes.
- [ ] Configurar recursos y réplicas.
- [ ] Configurar Service e Ingress.
- [ ] Documentar despliegue, rollback y diagnóstico.
- [ ] Validar en un clúster de prueba.

## Definition of Done

- [ ] El sistema se despliega en un clúster de prueba.
- [ ] Los probes y el rollback funcionan.
- [ ] No hay secretos en los manifiestos.
- [ ] La historia está enlazada desde el índice.

## Notas de alcance

- No implementa operadores, service mesh ni autoscaling avanzado.
- No reemplaza Docker Compose para desarrollo.
- No implementa alta disponibilidad de MongoDB.
