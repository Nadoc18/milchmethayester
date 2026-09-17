using UnityEngine;
using UnityEngine.UI;

namespace MNLTHII.Managers
{
    /// <summary>
    /// LA LEGENDE, SUR LE HUD PRINCIPAL.
    ///
    /// Elle etait au fond de l'ecran des consignes, c'est-a-dire a l'endroit exact ou
    /// elle ne sert a rien : on la voyait pendant l'ouverture du tour, et elle
    /// disparaissait au moment precis ou le joueur regarde le plateau et se demande ce
    /// qu'est cette forme-la.
    ///
    /// Elle vit maintenant sur le bandeau permanent, avec le compte des Shofars et
    /// l'Energie. On peut la consulter en jouant, sans rien ouvrir - c'est toute la
    /// difference entre une aide et un ecran d'aide.
    ///
    /// Elle vit dans le coin bas gauche, en grille de deux colonnes : un coin est le
    /// seul endroit d'un ecran qu'on peut occuper sans gener, et un bloc compact se
    /// balaie d'un regard - ce qu'une colonne de dix lignes ne permet pas.
    ///
    /// Note d'optimisation : remplie une seule fois, au premier tour ou les textes sont
    /// disponibles. Rien dans Update apres.
    /// </summary>
    public class HudLegend : MonoBehaviour
    {
        public const int MaxEntries = 10;

        [Header("Contenu")]
        public TMPro.TextMeshProUGUI headerText;
        public GameObject[] entryRoots = new GameObject[MaxEntries];
        public Image[] entryIcons = new Image[MaxEntries];
        public TMPro.TextMeshProUGUI[] entryNames = new TMPro.TextMeshProUGUI[MaxEntries];

        [Header("Aspect")]
        [Tooltip("Couleur des noms. Volontairement peu contrastee.")]
        public Color nameColor = new Color(0.62f, 0.69f, 0.85f, 0.85f);

        /// <summary>
        /// La couleur de chaque pictogramme, dans l'ordre du fichier. Les memes que
        /// partout ailleurs : c'est ce qui fait qu'une ligne de legende et une case du
        /// plateau se reconnaissent l'une l'autre.
        /// </summary>
        public Color[] iconColors = new Color[MaxEntries];

        private readonly LegendEntry[] _entries = new LegendEntry[MaxEntries];
        private bool _filled;

        private void Start()
        {
            // TeachingManager lit son fichier dans Awake ; Start garantit qu'il a fini.
            Fill();
        }

        public void Fill()
        {
            if (_filled) return;

            TeachingManager source = TeachingManager.Instance;
            if (source == null) return;

            int count = source.FillLegend(_entries);
            if (count <= 0) return;

            _filled = true;

            if (headerText != null) headerText.text = source.Label("legendHeader");

            for (int i = 0; i < MaxEntries; i++)
            {
                bool visible = (i < count && _entries[i] != null);

                if (entryRoots != null && i < entryRoots.Length && entryRoots[i] != null)
                    entryRoots[i].SetActive(visible);

                if (!visible) continue;

                if (entryIcons != null && i < entryIcons.Length && entryIcons[i] != null)
                {
                    Image icon = entryIcons[i];
                    icon.sprite = UI.IconLibrary.Get(Parse(_entries[i].icon));

                    if (iconColors != null && i < iconColors.Length && iconColors[i].a > 0.01f)
                        icon.color = iconColors[i];
                }

                if (entryNames != null && i < entryNames.Length && entryNames[i] != null)
                {
                    entryNames[i].text = _entries[i].name;
                    entryNames[i].color = nameColor;
                }
            }
        }

        /// <summary>
        /// Nom d'icone du fichier vers l'enum, sans Enum.Parse : celui-ci alloue et
        /// leve une exception sur une faute de frappe, ce qui ferait tomber le HUD
        /// entier pour un mot mal ecrit dans un JSON.
        /// </summary>
        private static UI.IconKind Parse(string key)
        {
            if (string.IsNullOrEmpty(key)) return UI.IconKind.None;

            switch (key)
            {
                case "Energy": return UI.IconKind.Energy;
                case "Factory": return UI.IconKind.Factory;
                case "Crystal": return UI.IconKind.Crystal;
                case "Tank": return UI.IconKind.Tank;
                case "Bunker": return UI.IconKind.Bunker;
                case "Command": return UI.IconKind.Command;
                case "Portal": return UI.IconKind.Portal;
                case "Crack": return UI.IconKind.Crack;
                case "Enemy": return UI.IconKind.Enemy;
                case "Base": return UI.IconKind.Base;
            }

            return UI.IconKind.None;
        }
    }
}
