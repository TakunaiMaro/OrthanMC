# Changelog

Todas las versiones relevantes de Orthanc Mc. El formato sigue
[Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y la versionado
[SemVer](https://semver.org/lang/es/).

## [1.0.1] - 2026-11-12

### Anadido

- El asistente de primera ejecucion empieza por la seleccion de idioma
  (English / Espanol). Esa pantalla se muestra siempre en ingles para que
  cualquier usuario pueda elegir antes de continuar.

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

### Corregido

- El fondo del tema **Custom Panorama** ahora se aplica de verdad: se compone
  el panorama con un velo oscuro y se pinta detras de toda la interfaz. Los
  controles con esquinas redondeadas reciben el recorte correspondiente para
  que no queden parches del color plano anterior.

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
