# Orthanc Mc

![Orthanc Mc](assets/logo-Orthanc-800.png)

Panel de escritorio para administrar un servidor local de Minecraft y su
tunel de [Playit](https://playit.gg). Reune en una sola ventana el estado del
servidor, la consola, los jugadores, las copias de seguridad, las versiones de
`server.jar`, los plugins/mods, los perfiles y la personalizacion de la app.

> Proyecto no oficial. No esta afiliado ni respaldado por Mojang Studios.

El proyecto esta desarrollado principalmente en espanol; la interfaz incluye
espanol e ingles.

## Caracteristicas

- **Perfiles**: varios servidores en una misma instalacion, con portada, horas
  de uso y cambio rapido entre ellos.
- **Estado en tiempo real**: proceso Java, RAM del sistema y del servidor,
  jugadores conectados y estado de Playit.
- **Consola**: comandos con historial y autocompletado.
- **Copias de seguridad**: manuales y automaticas del mundo o de jugadores.
- **Resets automaticos**: reinicio ciclico cada 1, 2, 3, 4, 6, 8, 12, 24, 48 o
  72 horas. Hace un backup previo, avisa a los jugadores por la action bar
  (10 min, 5 min, 1 min, 30 s y 5 a 1 s) y detiene y vuelve a iniciar el
  servidor. La cuenta atras solo corre con el servidor iniciado desde el panel.
- **Versiones**: instalacion de Vanilla, Snapshots, Paper, Purpur, Forge,
  NeoForge y Fabric desde el propio panel.
- **Plugins y mods**: activar/desactivar archivos `.jar` sin salir de la app.
- **Temas**: Default, Overworld, Nether y End.
- **Idiomas**: espanol e ingles, con archivos JSON editables. El asistente
  inicial usa el idioma de Windows (o ingles si es otro) y permite cambiarlo.
- **Actualizaciones**: comprueba la ultima release publicada en GitHub y
  ofrece el enlace de descarga.

### Deshabilitado por ahora

- **Backup en la nube**: la tarjeta aparece como *Proximamente*. La validacion
  de proveedores y la subida real todavia no estan disponibles.
- **Panoramas personalizados** (tema *Custom Panorama*): aparece como
  *Proximamente*. La rotacion y el escalado continuo del panorama resultan
  demasiado pesados; el codigo se conserva para una version futura.

## Requisitos

- Windows 10 u 11 de 64 bits.
- Java instalado para ejecutar servidores de Minecraft (el asistente inicial
  puede instalarlo por ti).
- Si usas el paquete *self-contained*, no necesitas instalar .NET.
  Si usas el paquete *framework-dependent*, necesitas
  [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).

## Instalacion

1. Descarga el ultimo `OrthancMC-vX.Y.Z-win-x64.zip` desde
   [Releases](https://github.com/TakunaiMaro/OrthanMC/releases).
2. Descomprime la carpeta en una ubicacion fija (por ejemplo
   `C:\Servers\OrthancMC`).
3. Ejecuta `McPanel.exe`. El paquete trae un unico ejecutable: las demas
   carpetas (`languages`, `themes`) solo contienen archivos editables.
4. La primera vez se abre el asistente, que:
   - muestra la seleccion de idioma en el idioma de Windows (espanol o ingles;
     si Windows usa otro idioma, se muestra en ingles);
   - configura Playit.GG para el tunel publico;
   - pregunta si ya tienes archivos del servidor o si quieres una instalacion
     limpia;
   - ofrece instalar el Java necesario para arrancar el servidor;
   - pide aceptar el EULA de Minecraft.

La configuracion se guarda en `%AppData%\McPanel`.

## Compilar en local

```powershell
dotnet build McPanel.csproj -c Release
dotnet run --project McPanel.csproj
```

Generar un paquete de release:

```powershell
pwsh ./scripts/build-release.ps1
# o, con PowerShell 5.1:
powershell -ExecutionPolicy Bypass -File .\scripts\build-release.ps1
```

Por defecto el paquete es *single-file* y *self-contained*: un solo
`McPanel.exe` (sin `.dll` sueltas) junto a las carpetas `languages/` y
`themes/` con contenido editable.

Opciones utiles:

```powershell
# Mantener el layout clasico con muchas .dll (para depurar)
powershell -ExecutionPolicy Bypass -File .\scripts\build-release.ps1 -MultiFile

# Paquete pequeno que requiere .NET 8 Desktop Runtime
powershell -ExecutionPolicy Bypass -File .\scripts\build-release.ps1 -FrameworkDependent
```

Salida: `dist/OrthancMC-v<version>-win-x64.zip`.

## Estructura del repositorio

```text
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

Los idiomas viven en `languages/*.json`. Cada clave es el texto base en
espanol y el valor es su traduccion. Para anadir uno nuevo copia
`languages/plantilla.json.example`, cambia `code` y `name`, y cargalo desde
*Configuracion de la App -> Idioma -> AGREGAR IDIOMA*.

## Aviso

Gran parte del codigo se genero con ayuda de IA, pero ha sido revisado por
personas. Si encuentras un fallo, abre un
[issue](https://github.com/TakunaiMaro/OrthanMC/issues).

## Licencia

Distribuido bajo la licencia MIT. Consulta [LICENSE](LICENSE).
