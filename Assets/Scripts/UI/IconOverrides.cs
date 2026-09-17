using UnityEngine;

namespace MNLTHII.UI
{
    /// <summary>
    /// TES PICTOGRAMMES A TOI.
    ///
    /// Glisse un fichier dans un de ces champs et il remplace le dessin genere, PARTOUT
    /// dans le jeu d'un seul coup : la legende du HUD, l'info-bulle, le popup des Tanks,
    /// l'ecran des consignes. Une seule fois, pas dix.
    ///
    /// Laisse un champ vide et le dessin genere par le code reste. Tu peux donc en
    /// remplacer trois et garder les sept autres : rien n'exige de tout faire d'un coup.
    ///
    /// CE QU'IL FAUT POUR QUE CA MARCHE BIEN
    ///
    /// Des fichiers carres, blancs sur fond transparent, importes en Sprite (2D and UI).
    /// Blancs parce que la couleur vient du code : le meme fichier sert en vert pour
    /// l'usine, en rouge pour le danger, en or pour l'Energie. Un pictogramme deja
    /// colore ne pourra plus changer de teinte, et il jurera partout ou la couleur
    /// signifie quelque chose.
    ///
    /// Pose par Milchemet > Construire le HUD sur l'objet des gestionnaires.
    /// </summary>
    public class IconOverrides : MonoBehaviour
    {
        [Header("Tes fichiers - blancs sur fond transparent")]
        public Sprite energy;
        public Sprite factory;
        public Sprite crystal;
        public Sprite tank;
        public Sprite bunker;
        public Sprite command;
        public Sprite portal;
        public Sprite crack;
        public Sprite enemy;
        public Sprite baseIcon;

        private void Awake()
        {
            Register();
        }

        /// <summary>
        /// Enregistre les dessins fournis. Appele aussi par OnValidate, pour que le
        /// changement se voie sans relancer la partie.
        /// </summary>
        public void Register()
        {
            IconLibrary.SetOverride(IconKind.Energy, energy);
            IconLibrary.SetOverride(IconKind.Factory, factory);
            IconLibrary.SetOverride(IconKind.Crystal, crystal);
            IconLibrary.SetOverride(IconKind.Tank, tank);
            IconLibrary.SetOverride(IconKind.Bunker, bunker);
            IconLibrary.SetOverride(IconKind.Command, command);
            IconLibrary.SetOverride(IconKind.Portal, portal);
            IconLibrary.SetOverride(IconKind.Crack, crack);
            IconLibrary.SetOverride(IconKind.Enemy, enemy);
            IconLibrary.SetOverride(IconKind.Base, baseIcon);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            Register();

            // Rafraichit ce qui est deja a l'ecran, pour voir le resultat tout de suite
            // en glissant un fichier - sans quoi il faudrait relancer pour juger.
            IconBinder[] binders = Object.FindObjectsByType<IconBinder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < binders.Length; i++) if (binders[i] != null) binders[i].Apply();
        }
#endif
    }
}
