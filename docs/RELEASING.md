# Publicar un release

El repositorio esta preparado para que GitHub Actions compile y publique el
paquete al empujar un tag `vX.Y.Z`.

## 1. Subir el proyecto a GitHub (primera vez)

```powershell
cd C:\Servers\Panel
git init -b main
git add .
git commit -m "Orthanc Mc 1.0.0"
git remote add origin https://github.com/TakunaiMaro/OrthanMC.git
git push -u origin main
```

Si el repositorio remoto ya tiene contenido, usa `git pull --rebase origin main`
antes del primer push.

## 2. Preparar la version

1. Edita `McPanel.csproj` y sube `Version`, `AssemblyVersion` y `FileVersion`.
2. Anade la entrada correspondiente en `CHANGELOG.md`.
3. Comprueba que `dotnet build McPanel.csproj -c Release` termina sin errores.

## 3. Crear el tag

```powershell
git add .
git commit -m "Release v1.0.1"
git tag v1.0.1
git push origin main
git push origin v1.0.1
```

El workflow `.github/workflows/release.yml` se encarga de:

1. Compilar con `scripts/build-release.ps1`.
2. Generar `OrthancMC-v1.0.1-win-x64.zip`.
3. Crear la release en GitHub con notas generadas automaticamente.

## 4. Alternativa manual

```powershell
pwsh ./scripts/build-release.ps1 -Version 1.0.1
```

Si no tienes PowerShell 7 (`pwsh`) instalado, usa el de Windows:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-release.ps1 -Version 1.0.1
```

Despues sube `dist/OrthancMC-v1.0.1-win-x64.zip` como asset de la release.

## Como encuentra las actualizaciones la app

`src/ReleaseConfig.cs` contiene `RepoOwner` y `RepoName`. La app consulta:

```text
https://api.github.com/repos/<owner>/<repo>/releases/latest
```

y compara `tag_name` con la version compilada. Por eso:

- El tag debe empezar por `v` y seguir `vX.Y.Z` (`v1.2.0`).
- No marques la release como *pre-release* si quieres que la app la detecte.
- Si el repositorio no tiene releases, la app muestra un aviso y sigue
  funcionando.
