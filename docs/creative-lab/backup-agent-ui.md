---
title: Nueva UI del configurador de copias
status: idea
depends_on: []
threads:
  - Propuesta UI configurador de copias | https://claude.ai/code/project/chan_01EF2Q1ZApiaHyNnYdzJpsNX?thread=cmsg_01EF2Q1ZApiaHyNnYdzJpsNXVqwRcKRV1Qa3XTKmG84s44
---
## Summary
Idea en debate: proponer una nueva interfaz para DinamicaBackupAgent.exe, el configurador de copias de seguridad de Eudora Quick Mail, que se abre en modo configuración con `/config /job <ruta del backup-job.json>`.
Propuesta en curso (maqueta de 6 pantallas, en inglés): Status con avisos que llevan a su solución, navegación lateral por tareas en vez de 6 pestañas, Schedule con las tres tareas juntas y una línea de tiempo, Source and destinations (carpeta de Eudora Quick Mail, destinos con el estado de su credencial y su acceso, retención), Restore test, Email reports e History.
El propietario la valora como una gran mejora sobre lo existente; sigue siendo una idea.
Maqueta: https://claude.ai/artifact/Y3yqAMPp3tktzHNY2a8tXM

## Decisions
- 2026-10-18 09:51 · Idea guardada en el laboratorio con el OK del propietario a su ruta.
- 2026-10-18 10:10 · Primera propuesta de UI publicada como maqueta, a partir de la versión 0.1.3 del configurador y de su backup-job.json; pendiente de debate.
- 2026-10-18 12:07 · Guardar es atómico: guarda la configuración y registra o actualiza la tarea de Windows en un solo paso; si algo falla, no cambia nada. Desaparece el botón aparte "Register / Update Windows task".
- 2026-10-18 12:07 · La UI del configurador va en inglés, como el resto de la aplicación.
- 2026-10-18 12:07 · El configurador es solo para Eudora Quick Mail: se quita el selector de aplicación. Se mantiene el campo de la carpeta de Eudora Quick Mail, en la pantalla Source and destinations, del que se detectan perfil, base de datos y ejecutable.
- 2026-10-18 12:07 · Maqueta actualizada: en inglés, con la carpeta de Quick Mail, "min" junto a su campo en Schedule, "Run it when the computer is back on" en dos líneas, y las pantallas Email reports e History.

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
9. **Advanced**: anular lo detectado (perfil, base de datos, ejecutable, argumentos, ficheros del perfil, carpetas de adjuntos), los comandos de PowerShell y "Recover interrupted restoration…".
10. Cada campo con un ancho acorde a su contenido y cada control con su tooltip.

En la maqueta, los resultados de la copia y de la ventana externa, el historial anterior a hoy y el resultado de Send test email son ilustrativos; el fallo de la prueba de hoy y de su aviso son los reales.
Restore a backup… y Advanced no tienen maqueta todavía.

### Pendiente
- Que el propietario revise la maqueta actualizada.
- Decidir si se convierte en borrador o se ejecuta; el código del configurador no está en este repositorio.
