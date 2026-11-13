# Changelog

Todas las versiones relevantes de Orthanc Mc. El formato sigue
[Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y la versionado
[SemVer](https://semver.org/lang/es/).

## [1.1] - 2026-11-12

### Anadido

- El asistente de primera ejecucion empieza por la seleccion de idioma
  (English / Espanol). La pregunta se muestra en el idioma de Windows: espanol
  si el sistema esta en espanol, ingles en cualquier otro caso. Elegir un
  idioma traduce el resto del asistente.
- **Resets automaticos** en *Configuracion del Servidor*: reinicio ciclico
  cada 1, 2, 3, 4, 6, 8, 12, 24, 48 o 72 horas. Antes del reinicio se crea un
  backup, se avisa a los jugadores por la action bar y el servidor se detiene y
  vuelve a iniciar solo. La cuenta atras muestra el tiempo restante, el proximo
  aviso y el ultimo reset, y solo corre mientras el servidor esta iniciado
  desde el panel.

### Cambiado

- El asistente ya no pregunta el nickname de admin: no se usaba en ninguna
  parte de la app. La configuracion lo conserva solo por compatibilidad.
- El paquete de release ahora es un unico `McPanel.exe` autocontenido y
  comprimido. En la raiz solo quedan el ejecutable, los logos y las carpetas
  `languages/` y `themes/` con contenido editable.
- `scripts/build-release.ps1` incorpora `-MultiFile` para recuperar el layout
  clasico y `-FrameworkDependent` para el paquete que requiere .NET 8.
- El empaquetado incluye `themes/` y los iconos como archivos externos
  (`ExcludeFromSingleFile`), que antes se perdian en modo single-file.

### Deshabilitado

- El tema **Custom Panorama** (panoramas personalizados) aparece como
  *Proximamente* y no se puede seleccionar: su rotacion y escalado continuo
  resultan demasiado pesados. El codigo se conserva para una version futura y
  las configuraciones existentes vuelven al tema Default.

## [1.0.0] - 2026-11-10

### Anadido

- Sistema de actualizaciones: consulta la ultima release de GitHub, compara
  versiones y ofrece abrir la pagina de descarga. Incluye opcion para buscar
  actualizaciones al iniciar.
- Estructura de repositorio para publicar en GitHub: `assets/`, `docs/`,
  `scripts/`, workflows de CI y release automatico por tag.
- Script `scripts/build-release.ps1` para compilar y empaquetar la app.
- Documentacion de instalacion, estructura y publicacion de releases.

### Cambiado

- Los logos e iconos se movieron a `assets/` y los archivos de desarrollo a
  `docs/`.
- La version de la aplicacion se define en `McPanel.csproj` y se muestra en la
  seccion de actualizaciones.
- Las rutas de desarrollo fijas (`C:\Servers\...`) se eliminaron de la carga
  de marca e idiomas: la app ahora usa solo su carpeta de ejecucion.

### Corregido

- El sondeo de estado ya no bloquea el hilo de interfaz: procesos, logs,
  Playit e IP se recogen en segundo plano. Esto elimina el aspecto de
  "cargando" durante 1-1,5 s al restaurar la ventana desde la barra de tareas.
- El tema Custom Panorama persiste entre reinicios: el ZIP se copia a
  `%AppData%\McPanel\panoramas` y se reconstruye al arrancar.
- El visor de panorama no regenera la imagen en cada cuadro al redimensionar.

### Deshabilitado

- Acceso a **Backup en la nube**. La tarjeta queda visible como *Proximamente*
  y la navegacion a esa seccion esta bloqueada.
