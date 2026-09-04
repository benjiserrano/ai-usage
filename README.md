# AI Usage

Widget de escritorio para la cuota restante de Codex CLI y Claude Code. Funciona en Windows y macOS desde un único código fuente (Avalonia).

Desde el menú del icono de bandeja, `Vista compacta` ancla un resumen al extremo inferior izquierdo. La vista completa y su posición se conservan al desactivarla.

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
