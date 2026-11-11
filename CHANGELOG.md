## [1.3.1] - 2026-11-11

- PATCH DH-20261111-PRICING-OPENING-ATTACHMENTS-084: backport compatible con master que permite hasta 30 archivos adjuntos en correo, conservando validación de nombres, contenido, MIME y seguridad SMTP.
- Coordinado con Pricing 1.10.1 y System 1.25.2; validación de CI y staging antes de integrar a master.

# Changelog

## [1.3.0] - 2026-11-10

- MINOR DH-20261110-CRM-LEAD-DUPLICATE-083: Convierte los eventos crm.lead.duplicate.v1 en notificaciones de sistema persistentes con el nombre del vendedor y del prospecto.
- Versión global Dhole 1.25.0. Validación CI y staging pendiente; sin autorización para despliegue de producción en este cambio.

## [1.2.1] - 2026-11-05

- Actualiza versión SemVer del componente y metadatos del ecosistema Dhole 1.3.1.
- Garantiza despliegues por push en master/develop y preserva las comprobaciones CI.

## [1.2.0] - 2026-11-05

- Incorporates universal Dhole SemVer policy with GitHub Actions checks.
- Synchronizes service assembly metadata with the ecosystem version.
- Adds permanent Codex instructions and coordinated release requirements.
