---
title: Nueva UI del configurador de copias
status: idea
depends_on: []
threads:
  - Propuesta UI configurador de copias | https://claude.ai/code/project/chan_01EF2Q1ZApiaHyNnYdzJpsNX?thread=cmsg_01EF2Q1ZApiaHyNnYdzJpsNXVqwRcKRV1Qa3XTKmG84s44
---
## Summary
Idea en debate: proponer una nueva interfaz para DinamicaBackupAgent.exe, el configurador de copias de seguridad de Eudora Quick Mail, que se abre en modo configuración con `/config /job <ruta del backup-job.json>`.
Primera propuesta (maqueta de 4 pantallas): una pantalla de Estado con avisos que llevan a su solución, navegación lateral por tareas en vez de 6 pestañas, la programación de las tres tareas juntas con una línea de tiempo, destinos con el estado de su credencial y su acceso, y la prueba de restauración con su carpeta y sus comprobaciones.
Maqueta: https://claude.ai/artifact/Y3yqAMPp3tktzHNY2a8tXM

## Decisions
- 2026-10-18 09:51 · Idea guardada en el laboratorio con el OK del propietario a su ruta.
- 2026-10-18 10:10 · Primera propuesta de UI publicada como maqueta, a partir de la versión 0.1.3 del configurador y de su backup-job.json; pendiente de debate.

## Requirements
- 2026-10-18 09:50 · Quiero que hagas una propuesta de UI de C:\docs\QuickMail\BackupAgent\DinamicaBackupAgent.exe, que es un configurador de copias de seguridad para un gestor de correo electrónico (Eudora Quick Mail). Para poder abrirlo en modo configuración, tienes que invocarlo así: C:\docs\QuickMail\BackupAgent\DinamicaBackupAgent.exe /config /job c:\docs\quickmail\data\backup-job.json
- 2026-10-18 09:51 · idea: Proponer una nueva UI para el configurador de copias de seguridad de Eudora Quick Mail (DinamicaBackupAgent.exe), que se abre en modo configuración con `DinamicaBackupAgent.exe /config /job <ruta del backup-job.json>`.
- 2026-10-18 10:10 · question: ¿En qué idioma va la UI del configurador? A) castellano, como la maqueta; B) inglés, como ahora; C) según el idioma de Windows. Recomendado: C, con castellano e inglés.
- 2026-10-18 10:10 · question: ¿Guardar debe registrar o actualizar también la tarea de Windows? A) sí, Guardar lo hace todo y solo pide la contraseña de Windows cuando haga falta; B) no, se mantiene un botón aparte como ahora, pero con aviso de "tarea desactualizada". Recomendado: A.

## Debate

### Lo que hay ahora (versión 0.1.3)
Ventana WPF de tamaño fijo con 6 pestañas (Application, External backup windows, Native Backup, Notifications, Restore test, History), todo en inglés.
- No hay ninguna vista de estado: para saber si las copias funcionan hay que ir a History, que aparece vacía aunque `backup-agent-last-run.json` registra una prueba fallida esta madrugada.
- Los errores no llevan a su solución: la prueba de las 04:00 falló por "Choose a restore-test directory first" y el aviso por correo de ese fallo tampoco se pudo enviar (el servidor SMTP no respondió); ninguna pantalla lo muestra.
- La programación está repartida en tres pestañas con los mismos 5 campos, y muestra "Día de la semana" y "Día del mes" aunque la frecuencia sea diaria.
- "Save configuration" no registra la tarea de Windows; hay que pulsar aparte "Register / Update Windows task…", que está en la pestaña Application.
- Pestañas largas con scroll, textos explicativos extensos, campos de ancho completo para valores de 2 a 5 caracteres (puerto, hora, porcentaje, copias a conservar), y "Cancel / Reopen application" como botón principal de ancho completo.

### Propuesta (maqueta)
1. **Estado** como pantalla inicial: un aviso por problema, con su causa y el botón que lo arregla (Elegir carpeta…, Enviar correo de prueba); una tarjeta por tarea con su último resultado, la próxima ejecución y su acción inmediata (Copiar ahora, Abrir ventana ahora, Probar ahora); las últimas operaciones.
2. **Navegación lateral** por tareas: Estado, Programación, Destinos y retención, Prueba de restauración, Avisos por correo, Historial; Restaurar una copia… y Avanzado aparte. Un punto rojo o ámbar marca la sección con problemas.
3. **Programación**: una tabla con las tres tareas (activar, frecuencia Diaria/Semanal/Mensual, hora, día solo si aplica, ejecutar al encender) y una línea de tiempo de 24 h que muestra cuándo está Quick Mail cerrado y si las tareas coinciden.
4. **Destinos y retención**: una tarjeta por destino con su carpeta, el estado de la credencial y el resultado de Probar acceso; las reglas de borrado de adjuntos en una frase; la retención 7/4/12 con un resumen ("hasta 23 copias, la más antigua de hace unos 12 meses") y una barra que lo dibuja.
5. **Prueba de restauración**: la carpeta de prueba arriba, marcada en rojo si falta, con una carpeta propuesta en un clic; qué se prueba; las consultas de comprobación como una tabla Nombre / SQL en lugar de texto "nombre | SELECT…".
6. **Barra inferior**: indica si hay cambios sin guardar; Guardar solo se activa con cambios; "Cerrar y volver a Quick Mail" sustituye a "Cancel / Reopen application".
7. **Avanzado**: lo detectado automáticamente (perfil, ejecutable, argumentos, ficheros del perfil, carpetas de adjuntos), los comandos de PowerShell y "Recuperar restauración interrumpida…".
8. Cada campo con un ancho acorde a su contenido y cada control con su tooltip.

En la maqueta, los resultados de la copia y de la ventana externa son ilustrativos; el fallo de la prueba y del aviso son los reales de hoy.
Avisos por correo, Historial, Restaurar y Avanzado no tienen maqueta todavía.

### Pendiente
- Que el propietario revise la maqueta y responda las preguntas de Requirements.
- Decidir si se convierte en borrador o se ejecuta; el código del configurador no está en este repositorio.
