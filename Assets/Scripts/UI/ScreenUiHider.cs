using System.Collections.Generic;
using UnityEngine;

namespace MNLTHII.Managers
{
    /// <summary>
    /// EFFACE TOUTE L'INTERFACE D'ECRAN, SAUF UNE.
    ///
    /// Deux moments du jeu n'ont qu'une chose a dire : le choix d'une cible ("quel
    /// ennemi ?") et la presentation du plateau. Pendant ces moments, le HUD -
    /// l'Energie, le rail, les objectifs, les boutons - n'est pas une aide, c'est un
    /// bruit : il noie la seule phrase qui compte, et le joueur ne sait pas qu'on
    /// attend quelque chose de lui.
    ///
    /// Ce qui s'efface : tous les Canvas d'ecran a la racine (Screen Space), sauf celui
    /// qu'on garde. Ce qui reste : les Canvas dans le monde - barres de vie, pastilles,
    /// marqueurs de cible - parce qu'ils font partie du plateau, pas du bandeau.
    ///
    /// Un seul appelant a la fois : Restore rend exactement ce que Hide a pris.
    /// </summary>
    public static class ScreenUiHider
    {
        private static readonly List<CanvasGroup> _groups = new List<CanvasGroup>(8);
        private static readonly List<float> _alpha = new List<float>(8);
        private static readonly List<bool> _raycasts = new List<bool>(8);

        public static bool Active { get { return _groups.Count > 0; } }

        public static void Hide(Canvas keep)
        {
            Restore();

            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null || canvas == keep) continue;
                if (canvas.renderMode == RenderMode.WorldSpace) continue;
                // Un canvas imbrique suit son parent : on ne traite que les racines.
                if (canvas.transform.parent != null && canvas.transform.parent.GetComponentInParent<Canvas>() != null) continue;
                // Celui qu'on garde peut etre un enfant d'un objet qui n'est pas un
                // canvas : on compare aussi les racines.
                if (keep != null && canvas.transform == keep.transform.root) continue;

                CanvasGroup group = canvas.GetComponent<CanvasGroup>();
                if (group == null) group = canvas.gameObject.AddComponent<CanvasGroup>();

                _groups.Add(group);
                _alpha.Add(group.alpha);
                _raycasts.Add(group.blocksRaycasts);

                group.alpha = 0f;
                group.blocksRaycasts = false;
            }
        }

        public static void Restore()
        {
            for (int i = 0; i < _groups.Count; i++)
            {
                CanvasGroup group = _groups[i];
                if (group == null) continue;
                group.alpha = _alpha[i];
                group.blocksRaycasts = _raycasts[i];
            }
            _groups.Clear();
            _alpha.Clear();
            _raycasts.Clear();
        }
    }
}
