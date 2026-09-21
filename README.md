# AI Usage

Widget de escritorio para la cuota restante de Codex CLI y Claude Code. Funciona en Windows y macOS desde un único código fuente (Avalonia).

Desde el menú del icono de bandeja, `Vista compacta` muestra un resumen inicialmente en extremo inferior izquierdo; puede arrastrarse y conserva su posición. La vista completa y su posición se conservan al desactivarla. Otro ajuste permite ocultar proveedores desconectados.

Ambas vistas se escalan arrastrando el tirador del borde derecho o con `Ctrl` + rueda: la tipografía, las barras y los márgenes crecen con el mismo factor, así que la proporción no cambia (el alto lo deduce el contenido). Cada vista recuerda su tamaño y `Tamaño por defecto` en el menú de bandeja lo devuelve al original.

## Desarrollo

```powershell
dotnet run
dotnet test tests/AIUsage.Tests.csproj
```

## Publicación

No existe un binario único para ambos sistemas: es el mismo proyecto publicado dos veces.

```powershell
dotnet publish -c Release -r win-x64 --self-contained -o portable/win
```

### Actualizaciones Windows

El ejecutable portable busca la última release de GitHub al abrirse. Si hay una versión superior, pide confirmación, descarga el `.exe`, valida su SHA-256 y se reinicia con la versión nueva. Cada release Windows debe publicar ambos archivos con estos nombres exactos:

```text
AIUsage-vX.Y.Z-win-x64.exe
AIUsage-vX.Y.Z-win-x64.exe.sha256
```

Genera el hash desde PowerShell:

```powershell
(Get-FileHash portable/release/AIUsage-vX.Y.Z-win-x64.exe -Algorithm SHA256).Hash.ToLower() + "  AIUsage-vX.Y.Z-win-x64.exe" |
  Set-Content -NoNewline portable/release/AIUsage-vX.Y.Z-win-x64.exe.sha256
```

macOS necesita además un bundle `.app` para salir en la barra de menús. `packaging/build-macos.sh` publica y lo monta, pero hay que ejecutarlo en un Mac porque usa `sips`, `iconutil` y `codesign`:

```bash
./packaging/build-macos.sh osx-arm64
```

Sin notarizar, Gatekeeper lo bloquea la primera vez: `xattr -dr com.apple.quarantine portable/mac/AIUsage.app`.

## Diferencias por sistema

Todo lo que varía está detrás de `IPlatform` (`Platform/`):

| | Windows | macOS |
|---|---|---|
| Arranque automático | clave `Run` del registro | `~/Library/LaunchAgents` |
| Ajustes | `%LOCALAPPDATA%\AIUsage` | `~/Library/Application Support/AIUsage` |
| Avisos | toast vía `powershell.exe` | `osascript` |
| Codex CLI | `PATH` + `PATHEXT`, `.cmd` vía `cmd.exe` | `PATH` + rutas de Homebrew, npm, bun y volta |
| Credenciales de Claude | `~/.claude/.credentials.json` | Keychain vía `security`, con el fichero como preferencia si existe |

## Notas

Codex usa `codex app-server`. Claude consulta su endpoint OAuth local; puede quedar `Stale` si Anthropic limita la consulta. No se guardan tokens.

En macOS, Claude Code guarda la sesión en el Keychain. La app la lee invocando `/usr/bin/security`, así que la primera lectura abre un diálogo del sistema; con `Permitir siempre` queda autorizado `/usr/bin/security` y no vuelve a preguntar.
