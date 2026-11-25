using Godot;

namespace AB;

/// <summary>
/// v0.24: personagens jogáveis. Todos usam o mesmo esqueleto (rig_base) e as mesmas animações (CharacterVisual);
/// muda só o .glb e os nomes das expressões na roda. A escolha é provisória (botão "Personagem" no menu principal,
/// salvo em UserSettings.Character) e vai pela rede (Hello → LobbyMember/RosterEntry.Character).
/// </summary>
public static class Characters
{
    public sealed class Def
    {
        public string Name, Model;
        /// <summary>Texto da roda por id de expressão (índice 0 vazio). Os objetos do .glb têm sempre os mesmos nomes
        /// (<see cref="Expressions.Node"/>); só o desenho e o rótulo mudam por personagem.</summary>
        public string[] FaceLabels;
    }

    public static readonly Def[] All =
    {
        new() { Name = "Rei", Model = "res://assets/models/rei.glb", FaceLabels = Expressions.Label },
        new()
        {
            Name = "Ametista", Model = "res://assets/models/ametista.glb",
            FaceLabels = new[] { "", "Relaxado", "Focado", "Sorriso convencido", "Bravo", "Rindo", "Surpreso" },
        },
    };

    public static int Count => All.Length;
    public static int Clamp(int id) => id >= 0 && id < All.Length ? id : 0;
    public static Def Get(int id) => All[Clamp(id)];
    public static string FaceLabel(int id, int face) => Get(id).FaceLabels[face];

    /// <summary>Personagem dos bots/outros jogadores: alterna pela vaga (provisório, até ter escolha de bots).</summary>
    public static int ForSlot(int slot) => Clamp(slot % All.Length);

    public static bool Available(int id) => ResourceLoader.Exists(Get(id).Model);
}
