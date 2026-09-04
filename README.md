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
dotnet publish -c Release -r win-x64   --self-contained -o portable/win
dotnet publish -c Release -r osx-arm64 --self-contained -o portable/mac
```

En macOS el binario hay que meterlo en un bundle `.app` para que aparezca en la barra de menús. Sin firmar ni notarizar, Gatekeeper lo bloquea al abrirlo la primera vez (`xattr -d com.apple.quarantine`, o clic derecho › Abrir).

## Diferencias por sistema

Todo lo que varía está detrás de `IPlatform` (`Platform/`):

| | Windows | macOS |
|---|---|---|
| Arranque automático | clave `Run` del registro | `~/Library/LaunchAgents` |
| Ajustes | `%LOCALAPPDATA%\AIUsage` | `~/Library/Application Support/AIUsage` |
| Avisos | toast vía `powershell.exe` | `osascript` |
| Codex CLI | `PATH` + `PATHEXT`, `.cmd` vía `cmd.exe` | `PATH` + rutas de Homebrew, npm, bun y volta |

## Notas

Codex usa `codex app-server`. Claude consulta su endpoint OAuth local; puede quedar `Stale` si Anthropic limita la consulta. No se guardan tokens.

En macOS, Claude Code guarda las credenciales en el Keychain y no en `~/.claude/.credentials.json`, así que el proveedor Claude aún no funciona allí.
