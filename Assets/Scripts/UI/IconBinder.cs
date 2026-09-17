using UnityEngine;
using UnityEngine.UI;

namespace MNLTHII.UI
{
    /// <summary>
    /// Pose le bon pictogramme sur une Image, AU DEMARRAGE et pas a la construction.
    ///
    /// POURQUOI CE DETOUR
    ///
    /// Le builder pourrait ecrire directement Image.sprite. Il le faisait, et c'etait
    /// un piege : le sprite genere se retrouvait enregistre dans la scene, donc le jour
    /// ou de vrais dessins arrivent, il aurait fallu reconstruire tout le HUD pour les
    /// voir - et tous ceux poses a la main auraient ete perdus au passage.
    ///
    /// Ici le builder ne pose qu'un marqueur : "cette image montre une usine". Le
    /// pictogramme est resolu au lancement, en passant par IconLibrary, qui donne la
    /// priorite aux dessins de IconOverrides. Deposer ses fichiers une seule fois suffit
    /// donc a changer TOUTE l'interface, sans reconstruire quoi que ce soit.
    ///
    /// Note d'optimisation : une seule resolution par image, dans Start. Rien en Update.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class IconBinder : MonoBehaviour
    {
        public IconKind kind = IconKind.None;

        [Tooltip("Optionnel : ce dessin-ci gagne sur tout le reste, pour cette image seulement.")]
        public Sprite overrideSprite;

        private void Start()
        {
            Apply();
        }

        /// <summary>Relit le pictogramme. Public pour pouvoir rafraichir apres coup.</summary>
        public void Apply()
        {
            Image image = GetComponent<Image>();
            if (image == null) return;

            Sprite sprite = IconLibrary.Get(kind, overrideSprite);
            if (sprite != null) image.sprite = sprite;
        }
    }
}
