using System.Collections.Generic;
using UnityEngine;

namespace MNLTHII.Managers
{
    /// <summary>
    /// LE LASER DU DUEL : un trait qui part du tireur et va a sa cible.
    ///
    /// Sans lui, une attaque se lisait a l'envers : un impact apparaissait sur une
    /// unite, un chiffre montait, et il fallait deviner QUI avait tire. Le trait
    /// relie les deux, et sa couleur dit le camp d'un coup d'oeil - BLEU pour un
    /// Tank, ROUGE-ORANGE pour un ennemi (les couleurs des deux camps dans tout le
    /// reste du jeu). Le Bunker garde son cyan, un peu plus clair.
    ///
    /// Le trait lui-meme est dessine par ShotTracer (le meme que le Bunker) ; ce
    /// fichier ne fait que choisir les deux extremites et la couleur. Il rend le
    /// temps de vol : l'appelant attend ce delai avant de faire tomber le coup, pour
    /// que l'impact arrive avec la tete du trait.
    /// </summary>
    public static class CombatTracer
    {
        public static readonly Color TankColor = new Color(0.30f, 0.66f, 1f, 1f);
        public static readonly Color EnemyColor = new Color(1f, 0.42f, 0.16f, 1f);

        private static readonly List<Renderer> _measure = new List<Renderer>();

        /// <summary>
        /// Tire le trait de l'attaquant vers sa cible (un pion, ou une structure).
        /// Rend la duree de vol en secondes reelles, 0 si rien n'a pu etre trace.
        /// </summary>
        public static float Fire(PawnController attacker, PawnController targetPawn, Hexagon targetHex)
        {
            if (attacker == null) return 0f;

            Vector3 end;
            if (targetPawn != null) end = CenterOf(targetPawn.transform, true);
            else if (targetHex != null) end = CenterOf(targetHex.transform, false);
            else return 0f;

            Vector3 start = CenterOf(attacker.transform, true);
            Color color = attacker.IsEnemy ? EnemyColor : TankColor;

            return ShotTracer.Fire(start, end, color);
        }

        /// <summary>
        /// Le centre du modele : la ou le trait part et ou il arrive. Les decals au
        /// sol, les particules et (pour une structure) les pions poses dessus ne
        /// comptent pas, sinon le trait visait le sol.
        /// </summary>
        private static Vector3 CenterOf(Transform t, bool isPawn)
        {
            t.GetComponentsInChildren(false, _measure);
            bool found = false;
            Bounds b = new Bounds(t.position, Vector3.zero);

            for (int i = 0; i < _measure.Count; i++)
            {
                Renderer r = _measure[i];
                if (r == null || r is ParticleSystemRenderer || r is SpriteRenderer) continue;
                if (r is LineRenderer || r is TrailRenderer) continue;
                if (r.GetComponent<ReadabilityDecal>() != null) continue;
                if (!isPawn && r.GetComponentInParent<PawnController>() != null) continue;

                if (!found) { b = r.bounds; found = true; }
                else b.Encapsulate(r.bounds);
            }
            _measure.Clear();

            if (!found) return t.position + Vector3.up * 0.5f;

            // Un peu au-dessus du centre : le trait passe par le corps, pas par les pieds.
            Vector3 c = b.center;
            c.y = Mathf.Max(c.y, b.min.y + b.size.y * 0.55f);
            return c;
        }
    }
}
