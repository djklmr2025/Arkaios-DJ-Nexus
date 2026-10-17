# Descargas web

La antigua pestaña del agente muestra ahora un navegador WebView2 con selector de las tres webs solicitadas. Pulsa Abrir para cargar la seleccionada. Incluye atrás, adelante, recargar y apertura en navegador externo. No carga páginas hasta pulsar Abrir ni realiza llamadas al agente.

Requiere Microsoft Edge WebView2 Runtime. El perfil se guarda en LocalAppData/ArkaiosDJ/WebDownloads. Las descargas conservan el comportamiento estándar de WebView2; no se envían automáticamente al Hub. Las ventanas emergentes no solicitadas se bloquean; los enlaces iniciados por el usuario se abren dentro del panel. No se inyectan scripts ni se automatizan formularios.

Validación pendiente en Windows: compilar ArkaiosDJAssistant.csproj, abrir la pestaña, probar los tres destinos, navegación, inicio de sesión y descargas autorizadas. Verificar también el mensaje de error sin Runtime. Este cambio no genera instalador ni modifica Karaoke Music Request System.
