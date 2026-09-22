using UnityEngine;
using UnityEngine.UI;
using MNLTHII;

namespace MNLTHII.Managers
{
    /// <summary>
    /// LE MENU D'UNE CASE : ce qui s'ouvre quand on clique une case du plateau.
    ///
    /// Trois choix :
    ///   - VOIR DE PRES : la camera descend sur la case (ou derriere le Tank qui s'y
    ///     trouve) - voir ImmersiveCamera.
    ///   - DETAILS : le panneau d'informations (prix, statistiques, ce que ca apporte,
    ///     et la rangee des evolutions avec leurs photos). Il ne s'ouvre plus au
    ///     survol : c'etait trop genant.
    ///   - UTILISER L'ENERGIE : l'action habituelle de la case (construire, poser un
    ///     Tank, ouvrir ses options). Son prix est affiche sur le bouton ; s'il n'y a
    ///     rien a faire ici, ou pas assez d'Energie, le bouton est eteint.
    ///
    /// POURQUOI UN MENU PLUTOT QU'UN CLIC DIRECT
    ///
    /// Avant, un clic depensait tout de suite. On ne pouvait donc jamais cliquer une
    /// case "pour voir" sans risquer de payer. Maintenant le clic ouvre, et c'est le
    /// deuxieme clic qui engage : on ne depense plus par accident.
    ///
    /// Le menu s'ouvre au-dessus du curseur. Il se referme avec Echap, un clic a cote,
    /// ou la fin de la phase de depense. Cliquer une autre case le deplace sur elle.
    ///
    /// Note d'optimisation : le composant est eteint quand le menu est ferme - aucun
    /// Update. Ouvert, il ne fait qu'un test de souris par frame. Aucune allocation :
    /// le prix passe par SetText, les libelles sont prepares par le builder.
    /// </summary>
    public class HexActionMenu : MonoBehaviour
    {
        public static HexActionMenu Instance;

        // =================================================================
        //  REFERENCES (posees par le HudBuilder, cablees ICI au lancement)
        // =================================================================
        [Header("Panneau")]
        public RectTransform panel;

        [Tooltip("Le voile sombre plein ecran derriere le menu, comme pour le choix du Tank.")]
        public GameObject dim;

        [Header("Voir de pres")]
        public Button viewButton;
        public TMPro.TextMeshProUGUI viewText;

        [Header("Details")]
        public Button detailsButton;
        public TMPro.TextMeshProUGUI detailsText;
        public string detailsLabel = "";

        [Header("Utiliser l'energie")]
        public Button useButton;
        public Image useBackground;
        public TMPro.TextMeshProUGUI useText;
        [Tooltip("Le prix, dans un champ NON RTL : en RTL, 50 deviendrait 05.")]
        public TMPro.TextMeshProUGUI costText;

        [Header("Libelles (hebreu, depuis hud_labels.json)")]
        public string viewLabel = "";
        public string useLabel = "";

        [Header("Aspect")]
        [Tooltip("Coche : le menu s'ouvre au centre de l'ecran. Decoche : au-dessus du point clique.")]
        public bool centered = false;
        public Vector2 cursorOffset = new Vector2(0f, 28f);
        public Color affordableColor = new Color(1f, 0.78f, 0.36f);
        public Color tooExpensiveColor = new Color(1f, 0.30f, 0.37f);
        public Color useIdleColor = new Color(1f, 0.78f, 0.36f, 0.14f);
        public Color useDisabledColor = new Color(0.49f, 0.55f, 0.69f, 0.06f);
        public Color textColor = new Color(0.90f, 0.93f, 1f);
        public Color textDisabledColor = new Color(0.49f, 0.55f, 0.69f);

        [Tooltip("Coche : 'utiliser l'energie' sur un batiment ouvre d'abord une confirmation "
               + "qui montre tous les niveaux et celui qu'on va atteindre.")]
        public bool confirmBuildings = true;

        // =================================================================
        //  ETAT
        // =================================================================
        private Hexagon _hex;
        private int _openedFrame = -1;
        private bool _closeRequested;

        private UnityEngine.Events.UnityAction _viewCallback;
        private UnityEngine.Events.UnityAction _useCallback;
        private UnityEngine.Events.UnityAction _detailsCallback;

        public bool IsOpen { get { return panel != null && panel.gameObject.activeSelf; } }

        /// <summary>Vrai quand la souris est sur le menu : ce clic-la est pour un bouton, pas pour le plateau.</summary>
        public bool PointerInside
        {
            get
            {
                return IsOpen && RectTransformUtility.RectangleContainsScreenPoint(panel, Input.mousePosition, null);
            }
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;

            _viewCallback = OnView;
            _useCallback = OnUse;

            if (viewButton != null) viewButton.onClick.AddListener(_viewCallback);
            if (useButton != null) useButton.onClick.AddListener(_useCallback);

            _detailsCallback = OnDetails;
            if (detailsButton != null) detailsButton.onClick.AddListener(_detailsCallback);

            Hide();
        }

        private void OnDestroy()
        {
            if (viewButton != null && _viewCallback != null) viewButton.onClick.RemoveListener(_viewCallback);
            if (useButton != null && _useCallback != null) useButton.onClick.RemoveListener(_useCallback);
            if (detailsButton != null && _detailsCallback != null) detailsButton.onClick.RemoveListener(_detailsCallback);

            if (Instance == this) Instance = null;
        }

        // =================================================================
        //  API
        // =================================================================
        public void Open(Hexagon hex)
        {
            if (hex == null || panel == null) return;

            _hex = hex;
            Fill(hex);

            if (dim != null) dim.SetActive(true);
            panel.gameObject.SetActive(true);
            Place();

            _openedFrame = Time.frameCount;
            _closeRequested = false;
            enabled = true;
        }

        public void Hide()
        {
            _hex = null;
            _closeRequested = false;

            if (panel != null && panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
            if (dim != null && dim.activeSelf) dim.SetActive(false);
            enabled = false;
        }

        // =================================================================
        //  BOUCLE (seulement menu ouvert)
        // =================================================================
        private void Update()
        {
            if (!IsOpen) { enabled = false; return; }

            TurnManager turns = TurnManager.Instance;
            if (turns == null || !turns.IsSpendingPhase || ImmersiveCamera.BlocksBoardInput || _hex == null)
            {
                Hide();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape)) { Hide(); return; }

            // Un clic hors du menu le ferme - sauf si ce meme clic tombe sur une autre
            // case, qui le rouvre aussitot sur elle (voir LateUpdate).
            if ((Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) && !PointerInside)
                _closeRequested = true;
        }

        private void LateUpdate()
        {
            if (!_closeRequested) return;
            _closeRequested = false;

            // Rouvert pendant cette frame par un clic sur une autre case : on garde.
            if (_openedFrame == Time.frameCount) return;

            Hide();
        }

        // =================================================================
        //  CONTENU
        // =================================================================
        private void Fill(Hexagon hex)
        {
            HexAction action = HexAction.Resolve(hex);

            // Rien a acheter ici : un Shofar, la Base, un ennemi, un batiment deja au
            // maximum. Le bouton reste visible mais eteint : on comprend qu'il n'y a
            // rien a faire, plutot que de se demander ou il est passe.
            bool usable = action.kind != HexActionKind.None
                          && action.kind != HexActionKind.MaxLevel
                          && action.kind != HexActionKind.Occupied;

            bool canUse = usable && action.affordable;

            if (useButton != null) useButton.interactable = canUse;
            if (useBackground != null) useBackground.color = canUse ? useIdleColor : useDisabledColor;

            if (useText != null)
            {
                useText.text = useLabel;
                useText.color = canUse ? textColor : textDisabledColor;
            }

            if (costText != null)
            {
                bool showCost = usable && action.cost > 0;
                costText.gameObject.SetActive(showCost);

                if (showCost)
                {
                    costText.SetText("{0}", action.cost);
                    costText.color = action.affordable ? affordableColor : tooExpensiveColor;
                }
            }

            // Details : il y a quelque chose a dire sur toute case qui a une action ou un
            // occupant. Rien sur un Shofar ou la Base, qui ont leur propre panneau.
            bool hasDetails = action.kind != HexActionKind.None && HexTooltipController.Instance != null;
            if (detailsButton != null) detailsButton.interactable = hasDetails;
            if (detailsText != null)
            {
                detailsText.text = detailsLabel;
                detailsText.color = hasDetails ? textColor : textDisabledColor;
            }

            bool canView = ImmersiveCamera.Instance != null;
            if (viewButton != null) viewButton.gameObject.SetActive(canView);
            if (viewText != null) viewText.text = viewLabel;
        }

        /// <summary>Au-dessus du curseur, sans jamais sortir de l'ecran.</summary>
        private void Place()
        {
            if (centered)
            {
                // Centre de l'ecran, quel que soit le pivot du panneau.
                Vector3 s = panel.lossyScale;
                float w = panel.rect.width * s.x;
                float h = panel.rect.height * s.y;
                Vector2 p = panel.pivot;

                panel.position = new Vector2(Screen.width * 0.5f - w * (0.5f - p.x),
                                             Screen.height * 0.5f - h * (0.5f - p.y));
                return;
            }

            Vector2 mouse = Input.mousePosition;
            Vector2 pos = mouse + cursorOffset;

            Vector3 scale = panel.lossyScale;
            float width = panel.rect.width * scale.x;
            float height = panel.rect.height * scale.y;

            // Pivot en bas au centre : le menu "sort" du point clique.
            float halfWidth = width * 0.5f;
            if (pos.x - halfWidth < 0f) pos.x = halfWidth;
            if (pos.x + halfWidth > Screen.width) pos.x = Screen.width - halfWidth;

            // Trop haut : on le passe sous le curseur.
            if (pos.y + height > Screen.height) pos.y = mouse.y - cursorOffset.y - height;
            if (pos.y < 0f) pos.y = 0f;

            panel.position = pos;
        }

        // =================================================================
        //  BOUTONS
        // =================================================================
        private void OnView()
        {
            Hexagon hex = _hex;
            Hide();

            if (hex != null) ImmersiveCamera.EnterHexView(hex);
        }

        private void OnDetails()
        {
            Hexagon hex = _hex;
            Hide();

            HexTooltipController details = HexTooltipController.Instance;
            if (hex != null && details != null) details.ShowDetails(hex);
        }

        private void OnUse()
        {
            Hexagon hex = _hex;
            Hide();

            // Construction : d'abord la confirmation (niveaux, prix). Les Tanks ont
            // deja leur propre ecran de choix, ils n'en ont pas besoin.
            if (hex != null && confirmBuildings && HexTooltipController.Instance != null)
            {
                HexAction action = HexAction.Resolve(hex);
                if (action.kind == HexActionKind.Build && HexTooltipController.Instance.ShowConfirm(hex))
                    return;
            }

            TurnManager turns = TurnManager.Instance;
            if (hex != null && turns != null) turns.ExecuteSpendingAction(hex);
        }
    }
}
