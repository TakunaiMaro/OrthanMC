# Orthanc Mc

Panel de escritorio para administrar un servidor local de Minecraft y su
tunel de [Playit](https://playit.gg).
Reúne en una sola App: el estado del servidor, la consola, los jugadores, las copias de seguridad, las versiones de
Minecraft con o sin los plugins/mods, los perfiles y la personalización de la app.

> Proyecto no oficial. No esta afiliado ni respaldado por Mojang Studios.

El proyecto esta desarrollado principalmente en español; Puedes cambiar entre español e ingles.

## Caracteristicas

- **Perfiles**: varios servidores en una misma instalacion, con portada, horas
  de uso y cambio rapido entre ellos.
- **Estado en tiempo real**: proceso Java, RAM del sistema y del servidor,
  jugadores conectados y estado de Playit.
- **Consola**: Comandos y funciones de Admin del server.
- **Copias de seguridad**: Posibilidad de realizar Backup manuales y automáticas del mundo o de jugadores.
- **Versiones**: instalacion de Vanilla, Snapshots, Paper, Purpur, Forge,
  NeoForge y Fabric desde el propio panel, el sistema identifica automáticamente que versión de Java necesitas y si no la tienes, la instala si lo permites.
- **Plugins y mods**: activar/desactivar archivos `.jar` sin salir de la app.
- **Temas**: Default, Overworld, Nether, End y panoramas personalizados (En desarrollo)
  (`.zip` con `panorama_0` a `panorama_5`).
- **Idiomas**: espanol e ingles, con archivos JSON editables y puedes añadir los que quieras.
- **Actualizaciones**: comprueba la ultima release publicada en GitHub y
  ofrece el enlace de descarga.

### Deshabilitado por ahora

- **Backup en la nube**: la tarjeta aparece como *Proximamente*. La validacion
  de proveedores y la subida real todavia no estan disponibles, para no depender del almacenamiento fisico.

## Requisitos

- Windows 10 u 11 de 64 bits.
- Java instalado para ejecutar servidores de Minecraft (el asistente inicial
  puede instalarlo por ti).
- Si usas el paquete *self-contained*, no necesitas instalar .NET.
  Si usas el paquete *framework-dependent*, necesitas
  [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).

## Instalacion

1. Descarga el ultimo `OrthancMC-vX.X.X-win-x64.zip` desde
   [Releases](https://github.com/TakunaiMaro/OrthanMC/releases).
2. Descomprime la carpeta en una ubicacion fija (por ejemplo
   `C:\Servers\OrthancMC`).
3. Ejecuta `McPanel.exe`.
4. La primera vez se abre la app, pregunta:
   - si ya tienes archivos del servidor o si quieres una instalacion
     limpia.
   - Ofrece instalar el Java necesario para arrancar el servidor.
   - Pide aceptar el EULA de Minecraft.

La configuracion y los panoramas personalizados se guardan en
`%AppData%\McPanel`.

## Estructura del repositorio

```
.
├── assets/                 Logos e iconos de la aplicacion
├── docs/                   Notas de desarrollo y publicacion
├── languages/              Archivos de idioma JSON (es, en)
├── scripts/                Scripts de compilacion y release
├── src/                    Codigo fuente (WinForms, .NET 8)
├── themes/                 Panorama de ejemplo (End.zip)
├── .github/workflows/      CI: build y release automatico
├── McPanel.csproj          Proyecto principal
└── app.manifest            Manifiesto de Windows
```

## Actualizaciones

La app consulta
`https://api.github.com/repos/TakunaiMaro/OrthanMC/releases/latest` y compara
el `tag_name` (`vX.Y.Z`) con la version compilada.

- Si hay una version nueva, muestra el nombre, las notas y un boton para abrir
  la release. **No descarga ni reemplaza archivos automaticamente.**
- La comprobacion al iniciar se puede desactivar desde
  *Configuracion de la App -> Actualizaciones*.
- Los repositorios sin releases publicadas se manejan sin errores.

Para publicar una version nueva consulta [docs/RELEASING.md](docs/RELEASING.md).

## Idiomas

Los idiomas estan en `languages/*.json`. Cada clave es el texto base en
español y el valor es su traduccion. Para anadir uno nuevo, copia
`languages/plantilla.json.example`, cambia `code` y `name`, y cargalo desde
*Configuracion de la App -> Idioma -> AGREGAR IDIOMA*.

Languages are stored in `languages/*.json`. Each key is the source text in
Spanish, and the value is its translation. To add a new one, copy
`languages/template.json.example`, change `code` and `name`, and upload it from
*Configuracion de la App -> Idioma -> AGREGAR IDIOMA*.
## Aviso

Gran parte del codigo se genero con ayuda de IA, pero ha sido revisado por
personas. Si encuentras un fallo, abre un
[issue](https://github.com/TakunaiMaro/OrthanMC/issues).

## Licencia

Distribuido bajo la licencia MIT. Consulta [LICENSE](LICENSE).
