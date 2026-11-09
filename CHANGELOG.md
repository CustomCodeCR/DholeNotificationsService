# Changelog

## [1.3.4] - 2026-11-08 (release candidato F3)

- Actualización PATCH de metadatos y versión global 1.13.13 asociada al ensayo automático HTTPS/WSS de Gateway, sin cambios funcionales adicionales; ramas principales intactas.


## [1.3.3] - 2026-11-08 (F6/F9 staging-audit candidate)

- Sincroniza Dhole Realtime `rt-20261107-01` a versión global `1.13.12` tras incorporar auditoría real de persistencia en staging. PATCH 1.3.2 → 1.3.3; no modifica rutas, eventos, datos, contratos REST ni flags. No se fusiona hasta completar gates formales.


## [1.3.2] - 2026-11-08 (candidato F9, sin publicar)

- Sincroniza release global `rt-20261107-01` a `1.13.11` tras resolver los hallazgos de CI de Config, Reports y Gateway. Bump PATCH de DholeNotificationsService `1.3.1` a `1.3.2`, sin modificar REST, eventos, precios ni datos; flags realtime OFF y PR F9 aún DRAFT.


## [1.3.1] - 2026-11-08 (candidato F9, sin publicación)

- Sincroniza metadatos de versión global del batch Dhole Realtime `rt-20261107-01` a `1.13.10`; actualización PATCH independiente de DholeNotificationsService `1.3.0` → `1.3.1`. No cambia contratos REST, datos, lógica de negocio ni flags; requiere gates F0–F8 y CI de SHA final antes de merge.


## [1.3.0] - 2026-11-08 (candidato F6.03, SIN PUBLICAR)

- Preserva el Hub de notificaciones SignalR existente y el inbox REST persistente; no duplica NotificationHub con el Gateway universal.
- Endurece asociación de conexiones a grupos autenticados de usuarios Guid válidos; prohíbe grupos de audiencia construidos desde claims arbitrarios.
- Relay Redis Pub/Sub a SignalR usa canal acotado y procesamiento secuencial, sin tareas fire-and-forget; ante saturación el inbox REST sigue siendo fuente autoritativa.
- Pruebas de aislamiento de destinatarios y rechazo de notificaciones inválidas con CI aislado, pendiente de gate real F6.03.
- Versión componente 1.2.1 -> 1.3.0, ecosistema global candidato 1.13.0; sin despliegues ni escritura en develop/master.

## [1.2.1] - 2026-11-05

- Consolida SemVer por componente y despliegues automáticos a staging/producción por push.
- Sincroniza versión nativa y manifiesto del release global Dhole.

## [1.2.0] - 2026-11-05

- Incorporates universal Dhole SemVer policy with GitHub Actions checks.
- Synchronizes service assembly metadata with the ecosystem version.
- Adds permanent Codex instructions and coordinated release requirements.
