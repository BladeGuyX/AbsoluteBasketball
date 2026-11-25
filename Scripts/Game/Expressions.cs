namespace AB;

/// <summary>
/// v0.21: expressões do rosto (objetos "Rosto_&lt;Nome&gt;" do modelo, art_source/scripts/make_rei.py).
/// Id 1..Count = posição na roda de expressões (0 = automática: o jogo escolhe pelo que o jogador está fazendo).
/// </summary>
public static class Expressions
{
    public const byte Serio = 1, Focado = 2, SorrisoFrio = 3, Bravo = 4, Feliz = 5, Surpreso = 6;
    public const int Count = 6;

    /// <summary>Nome do objeto no .glb (sem "Rosto_"), por id (índice 0 vazio).</summary>
    public static readonly string[] Node = { "", "Serio", "Focado", "SorrisoFrio", "Bravo", "Feliz", "Surpreso" };

    /// <summary>Texto da roda, por id.</summary>
    public static readonly string[] Label = { "", "Sério", "Focado", "Sorriso frio", "Bravo", "Feliz", "Surpreso" };
}
