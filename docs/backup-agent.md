---
title: Configurador de copias de seguridad
depends_on: []
threads:
  - Propuesta UI configurador de copias | https://claude.ai/code/project/chan_01EF2Q1ZApiaHyNnYdzJpsNX?thread=cmsg_01EF2Q1ZApiaHyNnYdzJpsNXVqwRcKRV1Qa3XTKmG84s44
---
## Summary
DinamicaBackupAgent.exe, el agente de copias de seguridad de Eudora Quick Mail: un ejecutable aparte que Quick Mail abre desde Tools → Backup and Restore (`/config /job <backup-job.json>`) y que la tarea de Windows ejecuta (`/scheduled`).
Hace copias ZIP completas de la base de datos y de los ficheros del perfil, un espejo de los adjuntos, ventanas para copias externas, pruebas de restauración, restauraciones y avisos por correo. Se coordina con Quick Mail mediante ficheros en el perfil (`.backup-request.json`, `.backup-response.json`, `.backup-access.lock`, `.backup-agent.lock`).
Código en el repositorio DinamicaTech/EudoraQuickMail, carpeta `Tools/` (`DinamicaBackup.Core`, `DinamicaBackupAgent` y sus tests); documentación técnica en `docs/backup-agent-implementation.md` de ese repositorio.
Hijos: `backup-agent/new-ui` (borrador de la nueva UI de configuración).

## Decisions
- 2026-10-18 12:42 · Nodo creado para el configurador de copias, al pasar a borrador la idea de su nueva UI.
- 2026-10-18 12:07 · Solo sirve a Eudora Quick Mail: se descarta usarlo para otras aplicaciones.

## Requirements
