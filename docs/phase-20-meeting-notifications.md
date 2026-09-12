# FASE 20 — Reuniones desde ContentService

DholeNotificationsService consume dos eventos publicados por el Outbox de DholeContentService en `dhole.notifications.events`.

## content.meeting.requested

Crea un mensaje de canal `Email` para Mercadeo. Los destinatarios se configuran exclusivamente en Notifications mediante la sección:

`Notifications:Meeting:MarketingRecipients`

En variables de entorno se puede usar `Notifications__Meeting__MarketingRecipients__0`, `__1`, etc. No se guardan direcciones de Mercadeo en ContentService.

## content.meeting.confirmed

Crea un mensaje Email dirigido a `ClientEmail` recibido en el evento de Content. El contenido incluye horario confirmado, zona horaria, modalidad y enlace de reunión cuando exista.

## Seguridad y responsabilidades

Los valores provenientes del evento se codifican antes de insertarse en HTML. Notifications conserva la responsabilidad exclusiva de la entrega SMTP, reintentos, estado y dead-letter. ContentService no conoce credenciales ni proveedores SMTP.

No se requieren cambios de esquema ni migraciones en Notifications para FASE 20.
