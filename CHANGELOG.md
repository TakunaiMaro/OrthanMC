# Changelog

Todas las versiones relevantes de Orthanc Mc. El formato sigue
[Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y la versionado
[SemVer](https://semver.org/lang/es/).

## Versión: 1.2.0

### Anadido

- Proveedor **Folia** en el canal *Plugins*, usando la API v3 de PaperMC
  (`fill.papermc.io`) con el ultimo build estable de cada version. La version
  instalada se detecta como Folia aunque comparta el lanzador paperclip de
  Paper, Folia permite crear servers con mejor optimización y gestión MultiCore.

### Cambiado

- Los avisos de reset de **10 y 5 minutos** se envian tambien por el chat,
  ademas de la action bar, para que no pasen desapercibidos.