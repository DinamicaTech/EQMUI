---
title: Nueva UI del configurador de copias
depends_on: []
threads:
  - 2026-10-18 09:51 · Propuesta UI configurador de copias | https://claude.ai/code/project/chan_01EF2Q1ZApiaHyNnYdzJpsNX?thread=cmsg_01EF2Q1ZApiaHyNnYdzJpsNXVqwRcKRV1Qa3XTKmG84s44
---
## Summary
Nueva interfaz de DinamicaBackupAgent.exe, el configurador de copias de seguridad de Eudora Quick Mail, que se abre en modo configuración con `/config /job <ruta del backup-job.json>`.
Implementada en la versión 0.2.0 (8 pantallas, en inglés): Status con avisos que llevan a su solución, navegación lateral por tareas en vez de 6 pestañas, Schedule con las tres tareas juntas y una línea de tiempo, Source and destinations (carpeta de Eudora Quick Mail, destinos con el estado de su credencial y su acceso, retención), Restore test, Email reports, History, Restore a backup y Advanced, con Save atómico.
Validada por el propietario el 2026-10-18; el código está en la copia de trabajo de este repositorio (`Tools/`) y queda pendiente devolverlo a EudoraQuickMail.
Maqueta: https://claude.ai/artifact/Y3yqAMPp3tktzHNY2a8tXM

## Decisions
- 2026-10-18 09:51 · Idea guardada en el laboratorio con el OK del propietario a su ruta.
- 2026-10-18 10:10 · Primera propuesta de UI publicada como maqueta, a partir de la versión 0.1.3 del configurador y de su backup-job.json; pendiente de debate.
- 2026-10-18 12:07 · Guardar es atómico: guarda la configuración y registra o actualiza la tarea de Windows en un solo paso; si algo falla, no cambia nada. Desaparece el botón aparte "Register / Update Windows task".
- 2026-10-18 12:07 · La UI del configurador va en inglés, como el resto de la aplicación.
- 2026-10-18 12:07 · El configurador es solo para Eudora Quick Mail: se quita el selector de aplicación. Se mantiene el campo de la carpeta de Eudora Quick Mail, en la pantalla Source and destinations, del que se detectan perfil, base de datos y ejecutable.
- 2026-10-18 12:07 · Maqueta actualizada: en inglés, con la carpeta de Quick Mail, "min" junto a su campo en Schedule, "Run it when the computer is back on" en dos líneas, y las pantallas Email reports e History.
- 2026-10-18 12:19 · Maqueta: Schedule recupera el marco de la tabla, la columna "If the computer was off" se ensancha para que su texto ocupe dos líneas y la línea de horas se ajusta al ancho de su marco; se añaden las pantallas Restore a backup y Advanced.
- 2026-10-18 12:40 · Restore a backup ajustado al motor real: base de datos, ficheros del perfil y adjuntos (todos o solo los elegidos) se restauran por separado, y los ficheros sustituidos se guardan en `.restore-previous-<run>`.
- 2026-10-18 12:42 · Leaves the lab: draft. Se mueve de creative-lab/backup-agent-ui a backup-agent/new-ui, hijo del nuevo nodo backup-agent.
- 2026-10-18 13:02 · Se implementa en este repositorio (EQMUI), en una copia del código del agente en `Tools/` (DinamicaBackup.Core, DinamicaBackupAgent y sus tests, copiados de EudoraQuickMail versión 0.1.3), para no tocar EudoraQuickMail ni el agente que usa Quick Mail. Cuando el propietario valide la nueva UI, se devuelve a DinamicaTech/EudoraQuickMail. Para probarla, el exe se compila en una carpeta aparte, nunca en C:\docs\QuickMail\BackupAgent. Sustituye a la ubicación que daba la línea draft (repositorio DinamicaTech/EudoraQuickMail).
- 2026-10-18 13:40 · Primera implementación en `Tools/DinamicaBackupAgent` (versión 0.2.0): las 8 pantallas con navegación lateral, Save atómico (escribe la configuración y registra la tarea de Windows solo si hace falta; si el registro falla, devuelve el fichero a como estaba), avisos con causa y acción en Status, y el historial que también muestra la última ejecución aunque no dejara recibo. El motor (`DinamicaBackup.Core`) no cambia. Los tests de UI de la 0.1.3, que comprobaban las 6 pestañas, se sustituyen por tests equivalentes sobre las nuevas pantallas, más tests del Save atómico, la navegación y los avisos.
- 2026-10-18 13:58 · El propietario valida la nueva UI (versión 0.2.0, probada desde C:\Temp\BackupAgentNuevo). Se integra en este repositorio; el siguiente paso es devolver el código a DinamicaTech/EudoraQuickMail.

## Requirements
- 2026-10-18 09:50 · Quiero que hagas una propuesta de UI de C:\docs\QuickMail\BackupAgent\DinamicaBackupAgent.exe, que es un configurador de copias de seguridad para un gestor de correo electrónico (Eudora Quick Mail). Para poder abrirlo en modo configuración, tienes que invocarlo así: C:\docs\QuickMail\BackupAgent\DinamicaBackupAgent.exe /config /job c:\docs\quickmail\data\backup-job.json
- 2026-10-18 09:51 · idea: Proponer una nueva UI para el configurador de copias de seguridad de Eudora Quick Mail (DinamicaBackupAgent.exe), que se abre en modo configuración con `DinamicaBackupAgent.exe /config /job <ruta del backup-job.json>`.
- 2026-10-18 10:10 · [answered 2026-10-18 12:07] question: ¿En qué idioma va la UI del configurador? A) castellano, como la maqueta; B) inglés, como ahora; C) según el idioma de Windows. Recomendado: C, con castellano e inglés.
- 2026-10-18 10:10 · [answered 2026-10-18 12:07] question: ¿Guardar debe registrar o actualizar también la tarea de Windows? A) sí, Guardar lo hace todo y solo pide la contraseña de Windows cuando haga falta; B) no, se mantiene un botón aparte como ahora, pero con aviso de "tarea desactualizada". Recomendado: A.
- 2026-10-18 12:07 · answer: idioma de la UI → En inglés ya que toda la aplicación está en inglés
- 2026-10-18 12:07 · answer: Guardar y tarea de Windows → Sí, guardar que actualice todo de forma atómica
- 2026-10-18 12:07 · Descarto la opción de que este sistema de copias funcione para más aplicaciones, pero sí es necesario poder indicar la carpeta de EQM para poder saber qué copiar
- 2026-10-18 12:07 · el 'min' de la pantalla de programación ponlo justo al lado del txtbox '240', así no ocupará una línea extra y quedará verticalmente mejor alineado
- 2026-10-18 12:07 · 'Ejecutarla al encenderlo' en dos líneas, no en tres
- 2026-10-18 12:07 · Faltan las pantallas de avisos por correo e historial.
- 2026-10-18 12:19 · El marco lo quité sin querer, puedes añadirlo de nuevo.
- 2026-10-18 12:19 · Como tenemos espacio en la pantalla 2, haz más ancha la columna 'It the computer was off' para que el texto de los checks ocupen solo dos lineas, no cinco como ahora
- 2026-10-18 12:19 · En esa misma pantalla, la barra de las horas es algo más ancha que el frame que las contiene.
- 2026-10-18 12:19 · Y ahora las pantallas de restore y advanced.
- 2026-10-18 12:42 · Ok, pásalo a borrador
- 2026-10-18 12:42 · answer: dónde va el borrador → Nodo propio (backup-agent, con el borrador como backup-agent/new-ui)
- 2026-10-18 13:02 · No podrías copiar el código a la carpeta de este proyecto y trabajarlo desde allí ? Para no tocar el proyecto actual ni interferir en EQM.
- 2026-10-18 13:02 · Ok, adelante
- 2026-10-18 13:02 · draft: Implementar en DinamicaBackupAgent (copia en este repositorio, carpeta Tools/DinamicaBackupAgent, que se devolverá a DinamicaTech/EudoraQuickMail al validarla) la nueva UI de configuración según la maqueta https://claude.ai/artifact/Y3yqAMPp3tktzHNY2a8tXM y las decisiones de este nodo, sin cambiar el motor ni el protocolo de ficheros con Quick Mail.

## Acceptance criteria
- Las 8 pantallas de la maqueta (Status, Schedule, Source and destinations, Restore test, Email reports, History, Restore a backup, Advanced) existen en el agente, en inglés, y leen y escriben el mismo backup-job.json que hoy sin perder campos.
- Save guarda la configuración y, solo cuando hace falta, registra o actualiza la tarea de Windows; si algo falla no cambia nada.
- No hay selector de aplicación; la carpeta de Eudora Quick Mail se elige en Source and destinations y de ella se detectan perfil, base de datos y ejecutable.
- Cada error visible (prueba de restauración, aviso por correo, acceso a destinos, tarea) explica la causa y ofrece la acción que lo arregla.
- Cada control tiene tooltip y cada campo un ancho acorde a su contenido.
- Los tests existentes de DinamicaBackupAgent.Tests y DinamicaBackup.Tests pasan en la comprobación automática de cada PR (Windows), y hay tests nuevos para el Save atómico y la navegación entre pantallas.

## Debate

### Lo que hay ahora (versión 0.1.3)
Ventana WPF de tamaño fijo con 6 pestañas (Application, External backup windows, Native Backup, Notifications, Restore test, History), todo en inglés.
- No hay ninguna vista de estado: para saber si las copias funcionan hay que ir a History, que aparece vacía aunque `backup-agent-last-run.json` registra una prueba fallida esta madrugada.
- Los errores no llevan a su solución: la prueba de las 04:00 falló por "Choose a restore-test directory first" y el aviso por correo de ese fallo tampoco se pudo enviar (el servidor SMTP no respondió); ninguna pantalla lo muestra.
- La programación está repartida en tres pestañas con los mismos 5 campos, y muestra "Día de la semana" y "Día del mes" aunque la frecuencia sea diaria.
- "Save configuration" no registra la tarea de Windows; hay que pulsar aparte "Register / Update Windows task…", que está en la pestaña Application.
- Pestañas largas con scroll, textos explicativos extensos, campos de ancho completo para valores de 2 a 5 caracteres (puerto, hora, porcentaje, copias a conservar), y "Cancel / Reopen application" como botón principal de ancho completo.

### Propuesta (maqueta, versión del 2026-10-18 12:07)
Textos en inglés (decisión 2026-10-18 12:07); las pantallas se describen aquí por su nombre en la maqueta.
1. **Status** como pantalla inicial: un aviso por problema, con su causa y el botón que lo arregla (Elegir carpeta…, Enviar correo de prueba); una tarjeta por tarea con su último resultado, la próxima ejecución y su acción inmediata (Copiar ahora, Abrir ventana ahora, Probar ahora); las últimas operaciones.
2. **Navegación lateral** por tareas: Status, Schedule, Source and destinations, Restore test, Email reports, History; Restore a backup… y Advanced aparte. Un punto rojo o ámbar marca la sección con problemas.
3. **Schedule**: una tabla con las tres tareas (activar, frecuencia Diaria/Semanal/Mensual, hora, día solo si aplica, ejecutar al encender) y una línea de tiempo de 24 h que muestra cuándo está Quick Mail cerrado y si las tareas coinciden.
4. **Source and destinations**: la carpeta de Eudora Quick Mail con Browse… y Detect again, y lo detectado de ella (perfil, base de datos, ejecutable); una tarjeta por destino con su carpeta, el estado de la credencial y el resultado de Probar acceso; las reglas de borrado de adjuntos en una frase; la retención 7/4/12 con un resumen ("hasta 23 copias, la más antigua de hace unos 12 meses") y una barra que lo dibuja.
5. **Restore test**: la carpeta de prueba arriba, marcada en rojo si falta, con una carpeta propuesta en un clic; qué se prueba; las consultas de comprobación como una tabla Nombre / SQL en lugar de texto "nombre | SELECT…".
6. **Email reports**: el fallo del último envío con su causa y la acción para reintentarlo; servidor, puerto y seguridad (STARTTLS / SSL/TLS) en un selector; la cuenta con el estado de su contraseña; remitente; destinatarios como etiquetas; Send test email con el resultado al lado y una corrección propuesta.
7. **History**: en hora local, filtros por tipo y "Problems only", duración y ficheros por ejecución; la fila seleccionada se despliega con la causa, el estado del aviso por correo, el id de la ejecución, la acción que lo arregla y Copy details; Export CSV….
8. **Barra inferior**: indica si hay cambios sin guardar; Save solo se activa con cambios y actualiza a la vez la tarea de Windows; "Close and reopen Quick Mail" sustituye a "Cancel / Reopen application".
9. **Restore a backup**: en tres pasos en una sola pantalla: elegir la copia (fecha, si se guarda como diaria, semanal o mensual, tamaño y si pasó una prueba de restauración), qué restaurar (base de datos, configuración y contactos, adjuntos que falten) y dónde (sustituir los datos actuales, guardando antes una copia de seguridad de ellos, o en otra carpeta para revisarla). La barra inferior resume lo que se va a hacer y Restore… pide confirmación. "Recover interrupted restoration…" pasa aquí, inactivo mientras no haya ninguna interrumpida.
10. **Advanced**: los valores detectados de la carpeta de Quick Mail (perfil, base de datos, ejecutable, argumentos) con "Reset all to detected"; los ficheros del perfil como etiquetas, marcando los que no existen (hoy groups.json y snooze.json); las carpetas de adjuntos; los tres comandos de PowerShell con su tiempo máximo; y la cuenta con la que se ejecuta la tarea de Windows.
11. Cada campo con un ancho acorde a su contenido y cada control con su tooltip.

En la maqueta, los resultados de la copia y de la ventana externa, el historial anterior a hoy, el resultado de Send test email y la lista de copias de Restore a backup son ilustrativos; el fallo de la prueba de hoy y de su aviso son los reales.
Todas las secciones tienen ya su maqueta.

### Contexto técnico (código del agente)
Leído el 2026-10-18 del proyecto completo de Eudora Quick Mail en el equipo del propietario (repositorio GitHub DinamicaTech/EudoraQuickMail), carpeta `Tools/` y `docs/backup-agent-implementation.md`.
- Proyectos: `DinamicaBackup.Core` (motor: ZIP, SQLite, catálogo, restauración, avisos), `DinamicaBackupAgent` (UI WPF construida por código en `AgentWindow*.cs`, credenciales, tarea de Windows) y sus tests. La nueva UI cambia solo `DinamicaBackupAgent`.
- Quick Mail lo abre desde Tools → Backup and Restore con `/config /job <json>`; la tarea programada lo ejecuta con `/scheduled`, y `/watch` reabre Quick Mail tras una ejecución sin sesión.
- Comunicación con Quick Mail por ficheros en el perfil: `.backup-access.lock`, `.backup-agent.lock`, `.backup-request.json` (petición con id y caducidad) y `.backup-response.json` (Quick Mail confirma y se cierra); `.restore-in-progress.json` bloquea el arranque tras una restauración interrumpida y `.backup-restored-profile.json` hace que arranque sin conexión. La nueva UI no toca este protocolo.
- La tarea de Windows consulta el JSON cada 5 minutos: cambiar horarios no exige volver a registrarla. Solo hace falta registrarla la primera vez o si cambia el ejecutable, el job o la cuenta, y entonces pide la contraseña de Windows (no se guarda). Así es como Save puede ser atómico sin pedir contraseña en cada guardado.
- Back up now y Test access solo funcionan si la tarea usa inicio de sesión con contraseña (evita el error 1219 de Windows); con una tarea interactiva, la UI debe desactivarlos y explicar por qué.
- Restauración real: base de datos, ficheros del perfil y adjuntos (todos o seleccionados) por separado; confirmación explícita; los ficheros sustituidos quedan en `.restore-previous-<run>`.
- El código aún contempla ProConta como aplicación futura; con la decisión de solo Eudora Quick Mail, la UI deja de mostrarlo.

### Pendiente
- Ejecutar el borrador en el repositorio DinamicaTech/EudoraQuickMail cuando el propietario lo pida.
