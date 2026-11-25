using System;
using Godot;

namespace AB;

/// <summary>Estrelas desenhadas (1–5), cheias em dourado.</summary>
public partial class StarsView : Control
{
    public int Stars;
    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;
    public override void _Draw()
    {
        float r = Mathf.Min(Size.Y * 0.5f, Size.X / 10f);
        for (int i = 0; i < 5; i++)
        {
            var c = new Vector2(r + i * r * 2.1f, Size.Y / 2f);
            var pts = new Vector2[10];
            for (int k = 0; k < 10; k++)
            {
                float a = -Mathf.Pi / 2 + k * Mathf.Pi / 5f;
                float rr = k % 2 == 0 ? r : r * 0.45f;
                pts[k] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
            }
            bool on = i < Stars;
            DrawColoredPolygon(pts, on ? new Color("ffc107") : new Color(0.75f, 0.78f, 0.84f));
            var loop = new Vector2[11];
            Array.Copy(pts, loop, 10); loop[10] = pts[0];
            DrawPolyline(loop, on ? new Color("b07d00") : new Color(0.6f, 0.63f, 0.7f), 1.5f, true);
        }
    }
}
