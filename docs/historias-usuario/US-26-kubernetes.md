# US-26 — Despliegue en Kubernetes

**Épica:** Infraestructura  
**Prioridad:** Baja (stretch)  
**Estimación:** 3 días  
**Requisito:** RNF-10 (ya cumplido por la stack actual) + RNF-20 (costo)  
**Depende de:** US-19, US-20, US-31  
**Estado:** Stretch  
**Decisión (2026-10-28):** no se implementa. Se mantiene documentada como mejora opcional fuera del MVP.

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

## Decisión de plataforma (2026-10-28)

**No se implementa Kubernetes.** La plataforma objetivo del proyecto es **Azure App Service** con contenedores de Docker Hub, desplegados por GitHub Actions.

### Por qué

1. **No hay requisito de escala.** FacturAR es un proyecto universitario con 1 a 3 aplicaciones consumidoras externas. El volumen esperado no justifica orquestación propia.
2. **Azure App Service ya da lo que se busca.** Reinicio automático con Always On, health check nativo, TLS y dominio automáticos, escalado por plan, configuración por App Settings y rollback por tag SHA.
3. **Kubernetes rompe el costo.** El control plane de AKS cuesta alrededor de USD 72 por mes, más nodos, balanceador y storage. RNF-20 exige recursos gratuitos o de bajo costo.
4. **Kubernetes no resuelve el cuello de botella real.** El límite para escalar horizontalmente el portal es Blazor Server con session affinity, que necesita un backplane de SignalR. Kubernetes no lo aporta.
5. **El CI/CD no necesita cambios.** Las imágenes ya se construyen con tags inmutables por SHA en Docker Hub, que es exactamente lo que Kubernetes consume. Si algún día hiciera falta, la migración no requiere tocar la aplicación.

### Qué se usa en su lugar

| Necesidad | Solución elegida |
| --- | --- |
| Reinicio ante caída | Always On + health check de App Service sobre `/health/live` |
| Sacar tráfico si no está sano | `healthCheckPath` apuntando a `/health/live` |
| Escalado | Scale up del plan de App Service y, si hiciera falta, Azure SignalR para el portal |
| Rollback | Job `rollback` del CD y modo manual por `workflow_dispatch` |
| Configuración y secretos | App Settings de Azure y secretos de GitHub Actions |
| Entornos paralelos | Deployment slots de App Service, sin costo adicional en el plan B1 |

### Cuándo reconsiderar

Esta historia se reactiva solo si aparece **una** de estas condiciones:

- Requisito formal de portabilidad on-premise o multi-nube.
- Volumen que obligue a ejecutar más de una réplica de la API con afinidad de sesión resuelta.
- Multi-tenant con aislamiento a nivel de clúster.
- Requisito de la cátedra que exija orquestación de contenedores.

## Notas de alcance

- No implementa operadores, service mesh ni autoscaling avanzado.
- No reemplaza Docker Compose para desarrollo.
- No implementa alta disponibilidad de MongoDB.
- La historia queda fuera del sprint y del grafo de dependencias del MVP.
