using System.Collections.Generic;
using UnityEngine;

namespace SomeonesHome
{
    /// <summary>
    /// Íconos del inventario dibujados por código (blanco sobre transparente, con borde suavizado):
    /// trazos simples con funciones de distancia, sin depender de sprites importados.
    /// </summary>
    public static class ItemIcons
    {
        const int Size = 96;
        static readonly Dictionary<ItemId, Texture2D> Cache = new();

        delegate float Shape(float x, float y);   // distancia con signo en píxeles (negativo = adentro)

        public static Texture2D Get(ItemId id)
        {
            if (id == ItemId.None) return null;
            if (Cache.TryGetValue(id, out var t) && t) return t;
            Shape shape = id switch
            {
                ItemId.Flashlight => Flashlight,
                ItemId.Phone => Phone,
                ItemId.TrashBag => Bag,
                ItemId.ShedKey => Key,
                ItemId.BusterCollar => Collar,
                ItemId.Backpack => Backpack,
                ItemId.Plates => Plates,
                _ => (x, y) => 1f,
            };
            t = Paint(shape, id.ToString());
            Cache[id] = t;
            return t;
        }

        static Texture2D Paint(Shape shape, string name)
        {
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                // 4 muestras por píxel: bordes limpios a cualquier escala de la UI.
                float a = 0f;
                for (int s = 0; s < 4; s++)
                    a += Mathf.Clamp01(0.5f - shape(x + 0.25f + (s & 1) * 0.5f, y + 0.25f + (s >> 1) * 0.5f));
                px[y * Size + x] = new Color32(255, 255, 255, (byte)(a * 0.25f * 255f));
            }
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = "Icono_" + name, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        // ── Formas ──
        static float Flashlight(float x, float y)
        {
            float body = Capsule(x, y, 18, 40, 56, 58, 8.5f);
            float head = Capsule(x, y, 58, 59, 70, 65, 13f);
            float lens = Capsule(x, y, 72, 66, 74, 67, 9f);
            float button = Circle(x, y, 36, 49, 3f);                                 // botón (hueco)
            float band = Mathf.Abs(Capsule(x, y, 55, 52, 61, 46, 0f)) - 1.1f;      // unión cuerpo/cabeza
            return Mathf.Max(Mathf.Min(body, Mathf.Min(head, lens)), -Mathf.Min(button, band));
        }

        static float Phone(float x, float y)
        {
            // Celular de barra de 1998: cuerpo, antena corta, pantallita y teclado.
            float body = RoundBox(x, y, 46, 42, 15, 30, 5);
            float antenna = RoundBox(x, y, 56, 76, 3, 7, 2);
            float shape = Mathf.Min(body, antenna);
            float screen = RoundBox(x, y, 46, 56, 10, 8, 1.5f);
            float keys = 1f;
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                    keys = Mathf.Min(keys, RoundBox(x, y, 39 + c * 7, 38 - r * 7, 2.2f, 2f, 0.8f));
            return Mathf.Max(shape, -Mathf.Min(screen, keys));                        // pantalla y teclas huecas
        }

        static float Bag(float x, float y)
        {
            float body = Circle(x, y, 48, 40, 25);
            float neck = RoundBox(x, y, 48, 66, 6, 6, 2);
            float knotL = Circle(x, y, 39, 76, 7);
            float knotR = Circle(x, y, 57, 76, 7);
            float shape = SmoothMin(SmoothMin(body, neck, 6f), Mathf.Min(knotL, knotR), 3f);
            float fold = Mathf.Abs(Capsule(x, y, 40, 30, 46, 55, 0f)) - 1.2f;         // pliegue
            return Mathf.Max(shape, -fold);
        }

        static float Key(float x, float y)
        {
            float bow = Mathf.Abs(Circle(x, y, 28, 56, 13)) - 4.5f;
            float shaft = Capsule(x, y, 40, 50, 80, 36, 4f);
            float t1 = Capsule(x, y, 66, 41, 62, 30, 3.5f);
            float t2 = Capsule(x, y, 75, 38, 72, 29, 3.5f);
            return Mathf.Min(bow, Mathf.Min(shaft, Mathf.Min(t1, t2)));
        }

        static float Collar(float x, float y)
        {
            float ring = Mathf.Abs(Ellipse(x, y, 48, 54, 28, 20)) - 4.5f;
            float tag = Circle(x, y, 48, 24, 9);
            float link = Capsule(x, y, 48, 34, 48, 30, 2f);
            float hole = Circle(x, y, 48, 24, 3f);
            return Mathf.Max(Mathf.Min(ring, Mathf.Min(tag, link)), -hole);
        }

        static float Backpack(float x, float y)
        {
            float body = RoundBox(x, y, 48, 42, 22, 26, 9);
            float handle = Mathf.Abs(Ellipse(x, y, 48, 70, 9, 7)) - 2.5f;
            float pocket = Mathf.Abs(RoundBox(x, y, 48, 30, 13, 9, 4)) - 1.4f;     // bolsillo (contorno hueco)
            float flap = Mathf.Abs(Capsule(x, y, 28, 52, 68, 52, 0f)) - 1.3f;       // costura de la tapa
            return Mathf.Max(Mathf.Min(body, handle), -Mathf.Min(pocket, flap));
        }

        static float Plates(float x, float y)
        {
            // Pila de platos de perfil: tres elipses planas apiladas.
            float a = Ellipse(x, y, 48, 34, 30, 5.5f);
            float b = Ellipse(x, y, 48, 46, 30, 5.5f);
            float c = Ellipse(x, y, 48, 58, 30, 5.5f);
            float gap1 = Mathf.Abs(y - 40f) - 1.2f, gap2 = Mathf.Abs(y - 52f) - 1.2f;
            return Mathf.Max(Mathf.Min(a, Mathf.Min(b, c)), -Mathf.Min(gap1, gap2));
        }

        // ── Distancias ──
        static float Circle(float x, float y, float cx, float cy, float r) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;

        static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float dx = (x - cx) / rx, dy = (y - cy) / ry;
            return (Mathf.Sqrt(dx * dx + dy * dy) - 1f) * Mathf.Min(rx, ry);
        }

        static float Capsule(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float pax = x - ax, pay = y - ay, bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / Mathf.Max(1e-4f, bax * bax + bay * bay));
            float dx = pax - bax * h, dy = pay - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        static float RoundBox(float x, float y, float cx, float cy, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(x - cx) - hx + r, qy = Mathf.Abs(y - cy) - hy + r;
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static float SmoothMin(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }
    }
}
