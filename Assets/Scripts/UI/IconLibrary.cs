using System.Collections.Generic;
using UnityEngine;

namespace MNLTHII.UI
{
    /// <summary>Les elements du jeu qui meritent un pictogramme.</summary>
    public enum IconKind
    {
        None,
        Energy,      // l'eclair : l'Energie
        Factory,     // la goutte : l'usine a Gaz
        Crystal,     // le losange : le Cristal
        Tank,        // le chevron : un Tank
        Bunker,      // le bouclier : le Bunker
        Command,     // l'etoile : le Centre de Commandement
        Portal,      // l'hexagone perce : le Shofar
        Crack,       // l'eclat : une fissure
        Enemy,       // le triangle inverse : le Yetzer Hara
        Base         // le carre plein : la Base
    }

    /// <summary>
    /// LES PICTOGRAMMES, DESSINES PAR LE CODE.
    ///
    /// Pourquoi generes plutot que dessines : le jeu a besoin d'une dizaine de symboles
    /// tout blancs, simples, de meme facture. Les faire a la main veut dire dix fichiers
    /// a produire, a importer, a regler et a garder coherents - et tant qu'ils
    /// n'existent pas, l'interface reste sans icone. Ici ils existent des le premier
    /// lancement, sans rien preparer.
    ///
    /// CE QUE C'EST, ET CE QUE CE N'EST PAS
    ///
    /// Ce sont des formes geometriques nettes, pas des illustrations. Un eclair, une
    /// goutte, un losange, un bouclier. Chaque champ d'icone de l'interface accepte un
    /// Sprite de l'inspecteur : si tu dessines mieux, glisse ton fichier et le tien
    /// gagne. Ces formes sont un plancher, pas un plafond.
    ///
    /// Tout est blanc pur : la couleur vient du composant Image qui l'affiche, donc le
    /// MEME pictogramme sert en vert pour l'usine, en rouge pour le danger, en or pour
    /// l'Energie, sans une seule texture de plus.
    ///
    /// Note d'optimisation : chaque icone est dessinee UNE fois, au premier appel, puis
    /// gardee dans un dictionnaire statique. Les textures survivent au rechargement de
    /// scene ; rien n'est redessine en cours de partie.
    /// </summary>
    public static class IconLibrary
    {
        private const int Size = 128;

        private static readonly Dictionary<IconKind, Sprite> _cache = new Dictionary<IconKind, Sprite>(12);

        /// <summary>
        /// Les dessins fournis par le projet, qui gagnent sur les formes generees.
        /// Remplis par IconOverrides - voir ce composant.
        /// </summary>
        private static readonly Dictionary<IconKind, Sprite> _overrides = new Dictionary<IconKind, Sprite>(12);

        /// <summary>
        /// Declare un dessin pour ce symbole. null efface la declaration et rend la
        /// main a la forme generee : on peut donc en retirer un sans tout casser.
        /// </summary>
        public static void SetOverride(IconKind kind, Sprite sprite)
        {
            if (kind == IconKind.None) return;

            if (sprite == null) _overrides.Remove(kind);
            else _overrides[kind] = sprite;
        }

        /// <summary>
        /// Rend le pictogramme demande, en le dessinant au premier appel.
        /// override a la priorite : c'est par la que tes propres dessins entrent.
        /// </summary>
        public static Sprite Get(IconKind kind, Sprite overrideSprite)
        {
            if (overrideSprite != null) return overrideSprite;
            return Get(kind);
        }

        public static Sprite Get(IconKind kind)
        {
            if (kind == IconKind.None) return null;

            // Un dessin fourni gagne toujours. C'est ce qui permet de remplacer les
            // formes generees par de vrais pictogrammes sans toucher une ligne de code.
            Sprite supplied;
            if (_overrides.TryGetValue(kind, out supplied) && supplied != null) return supplied;

            Sprite cached;
            if (_cache.TryGetValue(kind, out cached) && cached != null) return cached;

            Sprite created = Draw(kind);
            _cache[kind] = created;
            return created;
        }

        // =================================================================
        //  DESSIN
        // =================================================================
        /// <summary>
        /// Chaque icone est decrite par un POLYGONE en coordonnees -1..1, puis remplie
        /// avec un anticrenelage par distance. Un polygone est le seul format assez
        /// simple pour ecrire dix symboles lisibles sans moteur de dessin, et assez
        /// riche pour qu'ils ne se ressemblent pas tous.
        /// </summary>
        private static Sprite Draw(IconKind kind)
        {
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.name = "Icon_" + kind;

            Color32[] pixels = new Color32[Size * Size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 0);

            switch (kind)
            {
                case IconKind.Energy: FillPolygon(pixels, Bolt()); break;
                case IconKind.Factory: FillPolygon(pixels, Drop()); break;
                case IconKind.Crystal: FillPolygon(pixels, Diamond()); break;
                case IconKind.Tank: FillPolygon(pixels, Chevron()); break;
                case IconKind.Bunker: FillPolygon(pixels, Shield()); break;
                case IconKind.Command: FillPolygon(pixels, Star()); break;
                case IconKind.Enemy: FillPolygon(pixels, Triangle(false)); break;
                case IconKind.Base: FillPolygon(pixels, Square()); break;

                case IconKind.Portal:
                    // Un hexagone perce : l'anneau dit "porte", le trou dit "ouverte".
                    FillPolygon(pixels, Hexagon(0.96f));
                    CarvePolygon(pixels, Hexagon(0.52f));
                    break;

                case IconKind.Crack:
                    FillPolygon(pixels, Crack());
                    break;
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            return Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }

        // =================================================================
        //  LES FORMES
        // =================================================================
        private static Vector2[] Bolt()
        {
            return new Vector2[]
            {
                new Vector2( 0.28f,  0.92f), new Vector2(-0.46f,  0.04f),
                new Vector2(-0.06f,  0.04f), new Vector2(-0.28f, -0.92f),
                new Vector2( 0.46f,  0.06f), new Vector2( 0.06f,  0.06f)
            };
        }

        /// <summary>La goutte : pointe en haut, ventre rond en bas. L'usine a Gaz.</summary>
        private static Vector2[] Drop()
        {
            const int arc = 18;
            Vector2[] points = new Vector2[arc + 1];

            points[0] = new Vector2(0f, 0.94f);

            // Demi-cercle du bas, ouvert vers la pointe.
            for (int i = 0; i < arc; i++)
            {
                float t = i / (float)(arc - 1);
                float angle = Mathf.Lerp(0.62f, Mathf.PI * 2f - 0.62f, t) + Mathf.PI * 0.5f;
                points[i + 1] = new Vector2(Mathf.Cos(angle) * 0.68f, Mathf.Sin(angle) * 0.68f - 0.14f);
            }

            return points;
        }

        private static Vector2[] Diamond()
        {
            return new Vector2[]
            {
                new Vector2( 0f,     0.96f), new Vector2( 0.62f,  0.16f),
                new Vector2( 0f,    -0.96f), new Vector2(-0.62f,  0.16f)
            };
        }

        /// <summary>Le chevron : une pointe qui avance. Un Tank.</summary>
        private static Vector2[] Chevron()
        {
            return new Vector2[]
            {
                new Vector2( 0f,     0.86f), new Vector2( 0.84f, -0.52f),
                new Vector2( 0f,    -0.14f), new Vector2(-0.84f, -0.52f)
            };
        }

        /// <summary>Le bouclier : epaules droites, pointe en bas.</summary>
        private static Vector2[] Shield()
        {
            return new Vector2[]
            {
                new Vector2(-0.70f,  0.84f), new Vector2( 0.70f,  0.84f),
                new Vector2( 0.70f,  0.04f), new Vector2( 0.34f, -0.56f),
                new Vector2( 0f,    -0.92f), new Vector2(-0.34f, -0.56f),
                new Vector2(-0.70f,  0.04f)
            };
        }

        /// <summary>L'etoile a cinq branches : le Centre de Commandement.</summary>
        private static Vector2[] Star()
        {
            Vector2[] points = new Vector2[10];

            for (int i = 0; i < 10; i++)
            {
                float radius = (i % 2 == 0) ? 0.94f : 0.40f;
                float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                points[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            }

            return points;
        }

        private static Vector2[] Hexagon(float radius)
        {
            Vector2[] points = new Vector2[6];

            for (int i = 0; i < 6; i++)
            {
                float angle = Mathf.PI * 0.5f + i * Mathf.PI / 3f;
                points[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            }

            return points;
        }

        private static Vector2[] Triangle(bool up)
        {
            float s = up ? 1f : -1f;

            return new Vector2[]
            {
                new Vector2( 0f,     0.90f * s),
                new Vector2( 0.86f, -0.66f * s),
                new Vector2(-0.86f, -0.66f * s)
            };
        }

        private static Vector2[] Square()
        {
            return new Vector2[]
            {
                new Vector2(-0.72f,  0.72f), new Vector2( 0.72f,  0.72f),
                new Vector2( 0.72f, -0.72f), new Vector2(-0.72f, -0.72f)
            };
        }

        /// <summary>L'eclat : une ligne brisee epaisse, comme une fente qui s'ouvre.</summary>
        private static Vector2[] Crack()
        {
            return new Vector2[]
            {
                new Vector2( 0.10f,  0.96f), new Vector2( 0.34f,  0.30f),
                new Vector2( 0.04f,  0.16f), new Vector2( 0.30f, -0.96f),
                new Vector2(-0.02f, -0.30f), new Vector2( 0.24f, -0.14f),
                new Vector2(-0.08f,  0.36f), new Vector2(-0.16f,  0.96f)
            };
        }

        // =================================================================
        //  REMPLISSAGE
        // =================================================================
        /// <summary>
        /// Remplit un polygone, avec un bord adouci.
        ///
        /// On teste chaque pixel par la regle du nombre de croisements, puis on adoucit
        /// le bord d'apres sa distance au contour. Sans cet adoucissement les diagonales
        /// seraient en escalier - et toutes ces formes sont faites de diagonales.
        /// </summary>
        private static void FillPolygon(Color32[] pixels, Vector2[] polygon)
        {
            Paint(pixels, polygon, true);
        }

        /// <summary>Creuse un trou : meme test, mais on retire de l'alpha.</summary>
        private static void CarvePolygon(Color32[] pixels, Vector2[] polygon)
        {
            Paint(pixels, polygon, false);
        }

        private static void Paint(Color32[] pixels, Vector2[] polygon, bool add)
        {
            const float feather = 0.022f;      // en unites -1..1
            float half = Size * 0.5f;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float px = (x + 0.5f - half) / half;
                    float py = (y + 0.5f - half) / half;

                    bool inside = Contains(polygon, px, py);
                    float distance = EdgeDistance(polygon, px, py);

                    float alpha;
                    if (inside) alpha = Mathf.Clamp01(distance / feather);
                    else alpha = Mathf.Clamp01(1f - distance / feather);

                    if (alpha <= 0.001f) continue;

                    int index = y * Size + x;
                    byte current = pixels[index].a;

                    byte value = add
                        ? (byte)Mathf.Max(current, alpha * 255f)
                        : (byte)Mathf.Min(current, (1f - alpha) * 255f);

                    pixels[index] = new Color32(255, 255, 255, value);
                }
            }
        }

        /// <summary>Regle du nombre de croisements : un rayon vers la droite.</summary>
        private static bool Contains(Vector2[] polygon, float px, float py)
        {
            bool inside = false;

            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];

                if ((a.y > py) == (b.y > py)) continue;

                float t = (py - a.y) / (b.y - a.y);
                if (px < a.x + t * (b.x - a.x)) inside = !inside;
            }

            return inside;
        }

        /// <summary>Distance au segment le plus proche du contour.</summary>
        private static float EdgeDistance(Vector2[] polygon, float px, float py)
        {
            float best = float.MaxValue;
            Vector2 point = new Vector2(px, py);

            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                float d = SegmentDistance(point, polygon[j], polygon[i]);
                if (d < best) best = d;
            }

            return best;
        }

        private static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;

            if (lengthSq < 0.000001f) return Vector2.Distance(point, a);

            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSq);
            return Vector2.Distance(point, a + ab * t);
        }
    }
}
