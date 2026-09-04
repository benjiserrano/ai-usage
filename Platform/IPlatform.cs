namespace AIUsage;

/// <summary>Diferencias reales entre sistemas. Todo lo demás del proyecto es portable.</summary>
public interface IPlatform
{
    /// <summary>Carpeta donde vive settings.json, siguiendo la convención de cada sistema.</summary>
    string SettingsDirectory { get; }

    /// <summary>Texto del menú de bandeja para el arranque automático.</summary>
    string AutoStartLabel { get; }

    bool AutoStartEnabled { get; }

    void SetAutoStart(bool enabled);

    /// <summary>Localiza el CLI de Codex con las rutas y convenciones del sistema.</summary>
    CommandLaunch? FindCodex();
}
