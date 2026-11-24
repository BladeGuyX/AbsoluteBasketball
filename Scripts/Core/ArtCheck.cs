using System.Linq;
using Godot;

namespace AB;

/// <summary>
/// Etapa 7: mostra como a Godot importou um modelo do Blender (--inspect-model res://assets/models/x.glb):
/// tamanho, malhas, esqueleto (e se bate com o perfil humanoide do rig_base) e animações com o modo de loop.
/// </summary>
public static class ArtCheck
{
    public static void Inspect(string path)
    {
        if (!path.StartsWith("res://")) path = "res://" + path.Replace('\\', '/').TrimStart('/');
        var ps = ResourceLoader.Load<PackedScene>(path);
        if (ps == null) { GD.Print($"[ART] não carregou {path} (rode a importação: --headless --import)"); return; }
        var root = ps.Instantiate<Node3D>();
        GD.Print($"[ART] {path}");

        var aabb = new Aabb(); bool first = true;
        void Walk(Node n, Transform3D xf)
        {
            if (n is Node3D n3) xf = xf * n3.Transform;
            if (n is MeshInstance3D mi && mi.Mesh != null)
            {
                var b = xf * mi.Mesh.GetAabb();
                aabb = first ? b : aabb.Merge(b); first = false;
                GD.Print($"[ART]   malha {mi.Name}: {mi.Mesh.GetSurfaceCount()} superfície(s), pele={(mi.Skin != null ? "sim" : "não")}");
            }
            foreach (var c in n.GetChildren()) Walk(c, xf);
        }
        Walk(root, Transform3D.Identity);
        GD.Print($"[ART]   tamanho na Godot: largura {aabb.Size.X:0.000} · altura {aabb.Size.Y:0.000} · profundidade {aabb.Size.Z:0.000} m · base Y={aabb.Position.Y:0.000}");

        foreach (var sk in root.FindChildren("*", nameof(Skeleton3D), true, false).Cast<Skeleton3D>())
        {
            var profile = new SkeletonProfileHumanoid();
            var names = Enumerable.Range(0, sk.GetBoneCount()).Select(sk.GetBoneName).ToHashSet();
            var miss = Enumerable.Range(0, profile.BoneSize).Select(i => (string)profile.GetBoneName(i))
                .Where(b => !names.Contains(b) && !b.Contains("Thumb") && !b.Contains("Index") && !b.Contains("Middle") && !b.Contains("Ring") && !b.Contains("Little") && !b.Contains("Eye") && b != "Jaw")
                .ToList();
            GD.Print($"[ART]   esqueleto {sk.Name}: {sk.GetBoneCount()} ossos; humanoide sem dedos/olhos/mandíbula {(miss.Count == 0 ? "completo" : "faltando: " + string.Join(", ", miss))}");
        }
        foreach (var ap in root.FindChildren("*", nameof(AnimationPlayer), true, false).Cast<AnimationPlayer>())
            foreach (var name in ap.GetAnimationList())
            {
                var a = ap.GetAnimation(name);
                GD.Print($"[ART]   animação {name}: {a.Length:0.00} s, loop={a.LoopMode}");
            }
        root.Free();
    }
}
