# AI Usage

Widget Windows para cuota restante de Codex CLI y Claude Code.

Desde el menú del icono de bandeja, `Vista compacta` ancla un resumen al extremo inferior izquierdo. La vista completa y su posición se conservan al desactivarla.

## Desarrollo

```powershell
dotnet run
dotnet publish -c Release -r win-x64 --self-contained true -o portable
```

Codex usa `codex app-server`. Claude consulta su endpoint OAuth local; puede quedar `Stale` si Anthropic limita la consulta. No se guardan tokens.
