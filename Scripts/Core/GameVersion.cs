namespace AB;

/// <summary>
/// Versão do jogo — fonte única. O tools\release.ps1 lê <see cref="Number"/> para criar a tag/release "v{Number}"
/// no GitHub, e o <see cref="Updater"/> compara com a última release. Suba o número a cada entrega publicada.
/// </summary>
public static class GameVersion
{
    public const string Number = "0.42";
    public const string Stage = "Etapa 7";
    public static string Label => $"v{Number} · {Stage}";
}
