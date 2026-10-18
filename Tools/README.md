# Dinamica Backup Agent (copia de trabajo)

Copia del código del agente de copias de Eudora Quick Mail, tomada de DinamicaTech/EudoraQuickMail (`Tools/`, versión 0.1.3), para implementar la nueva UI del nodo `docs/backup-agent/new-ui.md` sin tocar ese repositorio.

- `DinamicaBackup.Core`: motor (copias, restauración, catálogo, avisos por correo). No se cambia.
- `DinamicaBackupAgent`: el exe WPF (`/config`, `/scheduled`). Aquí va la nueva UI.
- `DinamicaBackup.Tests`, `DinamicaBackupAgent.Tests`: tests (xUnit v3).

Compilar y probar (Windows, .NET 8): `dotnet test Tools/DinamicaBackup.Tests` y `dotnet test Tools/DinamicaBackupAgent.Tests`.
Para probar el exe, publicarlo en una carpeta aparte; nunca en la carpeta del agente que usa Quick Mail.
Cuando la nueva UI se valide, estos proyectos vuelven a DinamicaTech/EudoraQuickMail.
