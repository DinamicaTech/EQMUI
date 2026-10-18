---
title: Rediseño de la pantalla principal
status: idea
depends_on: []
threads:
  - Rediseño pantalla principal | https://claude.ai/code/project/chan_01GmFabmbZMkMrshz1aTJxzL?thread=cmsg_01GmFabmbZMkMrshz1aTJxzLEUPD1BVbiaFMLBK9DwXWun
---
## Summary
Idea en debate: rediseñar desde cero la pantalla principal (Home) de Eudora Quick Mail para que muestre de un vistazo el estado de las cuentas de correo, de las copias de seguridad y de las pruebas de restauración, las estadísticas de correo y otra información útil.
Problemas de la pantalla actual (v0.8.86.7): todo es texto y tablas planas sin un estado general; la sección de copias sale entera en rojo aunque la copia fue bien y solo falla la prueba de restauración; el envío (SMTP) aparece como "Not yet checked" en todas las cuentas; las estadísticas son de una sola cuenta y sin tendencia.
Propuesta en debate (maqueta, en inglés como la aplicación): un aviso general arriba con lo que necesita atención y el botón que lo arregla; cuatro tarjetas de estado (cuentas, última copia, prueba de restauración, correo saliente); tabla de cuentas con recepción, envío y un botón Test por cuenta; actividad de correo con comparación de hoy frente a la media diaria; columna lateral con copias, agenda de hoy y carpetas a revisar.
Maqueta: https://claude.ai/artifact/GMCWNVawTcf1Ex6pKksSJn

## Decisions
- 2026-10-18 13:12 · Idea guardada en el laboratorio con el OK del propietario a su ruta. El propietario da total libertad creativa: la pantalla puede rediseñarse desde cero.
- 2026-10-18 13:25 · Primera propuesta publicada como maqueta, con los datos de la captura de la pantalla actual (v0.8.86.7); pendiente de debate.

## Requirements
- 2026-10-18 13:10 · Rediseño de la pantalla principal de Eudora Quick Mail, en la que se tienen que mostrar datos relevantes (por ejemplo, que las cuentas de correo estén funcionando bien), resultado de copias de seguridad / restore, datos estadísticos de correos y otra información que consideres útil.
- 2026-10-18 13:10 · idea: Rediseño de la pantalla principal (Home) de Eudora Quick Mail para que muestre de un vistazo los datos relevantes: si las cuentas de correo funcionan bien, el resultado de las copias de seguridad y de las pruebas de restauración, estadísticas de correo y otra información útil.
- 2026-10-18 13:12 · Tienes total libertad creativa, puedes rediseñar la pantalla desde cero.
- 2026-10-18 13:25 · question: ¿Las estadísticas de correo muestran por defecto todas las cuentas juntas o una cuenta? A) todas juntas, con selector para ver una; B) una cuenta, como ahora. Recomendado: A.
- 2026-10-18 13:25 · question: ¿Añadimos un gráfico de los últimos 14 o 30 días de correo recibido y enviado? Necesita guardar o calcular el recuento por día. A) sí, 14 días; B) no, basta con hoy frente a la media. Recomendado: A.
- 2026-10-18 13:25 · question: ¿Se prueba el envío (SMTP) de las cuentas automáticamente, por ejemplo una vez al día, solo conectando y autenticando sin enviar nada? A) sí, a diario; B) no, solo con el botón Test. Recomendado: A.
