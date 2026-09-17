using UnityEngine;
using UnityEngine.UI;
using MNLTHII.Rules;

namespace MNLTHII.Managers
{
    /// <summary>
    /// LE CONSEILLER, cote ecran.
    ///
    /// Trois cartes au plus, en bas a droite, pendant la phase de depense. Chacune dit
    /// QUOI faire, POURQUOI maintenant, et COMBIEN ca coute. Au survol, un Tank
    /// translucide joue l'action sur le plateau et la camera va la cadrer ; au clic,
    /// l'action est jouee pour de bon.
    ///
    /// POURQUOI CE PANNEAU EXISTE
    ///
    /// La boucle du jeu n'est pas devinable en jouant : tuer des ennemis use le Shofar
    /// qui les a deployes, quatre morts brisent son bouclier, et c'est seulement
    /// pendant ces quatre tours-la qu'un Tank peut le fermer sans y passer dix tours.
    /// Un joueur qui ignore ce lien defend parfaitement et ne gagne jamais. Le
    /// conseiller rend ce lien visible au moment ou il compte.
    ///
    /// Il ne joue pas a la place du joueur : il propose, il explique, et il se tait
    /// quand il n'a rien d'utile a dire - un panneau qui parle tout le temps ne se lit
    /// plus au bout de trois tours.
    ///
    /// Note d'optimisation : les conseils sont recalcules aux changements d'etat
    /// (ouverture de la phase, action jouee), jamais dans Update. Le tableau de
    /// conseils, les delegues de bouton et les chaines de libelles sont construits une
    /// fois ; l'affichage n'alloue que pour les nombres, via SetText.
    /// </summary>
    public class AdvisorPanel : MonoBehaviour
    {
        public static AdvisorPanel Instance;

        public const int MaxCards = AdvisorRules.MaxSuggestions;

        // =================================================================
        //  REFERENCES
        // =================================================================
        [Header("Racine")]
        public GameObject panelRoot;

        [Header("Entete")]
        public TMPro.TextMeshProUGUI headerText;

        [Header("Les cartes")]
        public Button[] cardButtons = new Button[MaxCards];
        public GameObject[] cardRoots = new GameObject[MaxCards];
        public Image[] cardBackgrounds = new Image[MaxCards];
        public Image[] cardAccents = new Image[MaxCards];
        public TMPro.TextMeshProUGUI[] cardTitles = new TMPro.TextMeshProUGUI[MaxCards];
        public TMPro.TextMeshProUGUI[] cardReasons = new TMPro.TextMeshProUGUI[MaxCards];
        public TMPro.TextMeshProUGUI[] cardCosts = new TMPro.TextMeshProUGUI[MaxCards];

        // =================================================================
        //  LIBELLES
        // =================================================================
        [Header("Libelles")]
        public string header = "";
        public string titleCreateTank = "";
        public string titleChangeStance = "";
        public string titleEvolve = "";
        public string titleBunker = "";
        public string titleCrystal = "";
        public string titleCommand = "";
        public string titleBuild = "";

        /// <summary>Une phrase par identifiant de raison d'AdvisorRules.</summary>
        public string[] reasons = new string[AdvisorRules.REASON_COUNT];

        /// <summary>Garde, Assaut, Chasse - repris de unit.stances.</summary>
        public string[] stanceNames = new string[3];

        // =================================================================
        //  REGLAGES
        // =================================================================
        [Header("Comportement")]
        /// <summary>La camera va cadrer l'action survolee.</summary>
        public bool focusOnHover = true;

        /// <summary>Le fantome joue l'action survolee sur le plateau.</summary>
        public bool ghostOnHover = true;

        [Header("Couleurs")]
        public Color cardIdleColor = new Color(0.49f, 0.63f, 1f, 0.06f);
        public Color cardHoverColor = new Color(0.49f, 0.63f, 1f, 0.18f);
        public Color costColor = new Color(1f, 0.78f, 0.36f);
        public Color urgentColor = new Color(1f, 0.30f, 0.37f);
        public Color calmColor = new Color(0.24f, 0.88f, 0.82f);

        // =================================================================
        //  ETAT INTERNE
        // =================================================================
        private readonly AdvisorSuggestion[] _suggestions = new AdvisorSuggestion[MaxCards];
        private int _count;

        private UnityEngine.Events.UnityAction[] _callbacks;
        private int _hovered = -1;

        private void Awake()
        {
            if (Instance == null) Instance = this;

            BindButtons();
            Hide();
        }

        private void BindButtons()
        {
            if (cardButtons == null) return;

            _callbacks = new UnityEngine.Events.UnityAction[cardButtons.Length];

            for (int i = 0; i < cardButtons.Length; i++)
            {
                if (cardButtons[i] == null) continue;

                int slot = i;                 // capture unique, a la construction
                _callbacks[i] = delegate { Apply(slot); };
                cardButtons[i].onClick.AddListener(_callbacks[i]);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (cardButtons == null || _callbacks == null) return;

            for (int i = 0; i < cardButtons.Length && i < _callbacks.Length; i++)
                if (cardButtons[i] != null && _callbacks[i] != null)
                    cardButtons[i].onClick.RemoveListener(_callbacks[i]);
        }

        // =================================================================
        //  API
        // =================================================================
        /// <summary>
        /// Recalcule les conseils et les affiche. Appele a l'ouverture de la phase de
        /// depense et apres chaque action : une action change le plateau, donc elle
        /// change ce qu'il est urgent de faire ensuite.
        /// </summary>
        public void Refresh()
        {
            _count = AdvisorRules.Build(_suggestions);

            if (_count <= 0) { Hide(); return; }

            if (panelRoot != null) panelRoot.SetActive(true);
            if (headerText != null) headerText.text = header;

            for (int i = 0; i < MaxCards; i++) FillCard(i, i < _count ? _suggestions[i] : null);
        }

        public void Hide()
        {
            ClearPreview();
            _hovered = -1;
            _count = 0;

            if (panelRoot != null && panelRoot.activeSelf) panelRoot.SetActive(false);
        }

        /// <summary>Survol : appele par les relais poses sur chaque carte.</summary>
        public void SetHovered(int slot, bool hovered)
        {
            if (slot < 0 || slot >= _count) return;

            if (cardBackgrounds != null && slot < cardBackgrounds.Length && cardBackgrounds[slot] != null)
                cardBackgrounds[slot].color = hovered ? cardHoverColor : cardIdleColor;

            if (hovered)
            {
                _hovered = slot;
                Preview(_suggestions[slot]);
            }
            else if (_hovered == slot)
            {
                _hovered = -1;
                ClearPreview();
            }
        }

        // =================================================================
        //  AFFICHAGE
        // =================================================================
        private void FillCard(int slot, AdvisorSuggestion suggestion)
        {
            bool visible = (suggestion != null);

            if (cardRoots != null && slot < cardRoots.Length && cardRoots[slot] != null)
                cardRoots[slot].SetActive(visible);

            if (!visible) return;

            if (cardBackgrounds != null && slot < cardBackgrounds.Length && cardBackgrounds[slot] != null)
                cardBackgrounds[slot].color = cardIdleColor;

            // La couleur du liseret dit l'URGENCE d'un coup d'oeil, avant meme de lire.
            Color accent = AccentFor(suggestion);

            if (cardAccents != null && slot < cardAccents.Length && cardAccents[slot] != null)
                cardAccents[slot].color = accent;

            if (cardTitles != null && slot < cardTitles.Length && cardTitles[slot] != null)
            {
                cardTitles[slot].text = TitleFor(suggestion);
                cardTitles[slot].color = accent;
            }

            if (cardReasons != null && slot < cardReasons.Length && cardReasons[slot] != null)
                cardReasons[slot].text = ReasonFor(suggestion);

            if (cardCosts == null || slot >= cardCosts.Length || cardCosts[slot] == null) return;

            cardCosts[slot].SetText("{0}", suggestion.cost);
            cardCosts[slot].color = costColor;
        }

        /// <summary>
        /// Le titre reprend la posture quand il y en a une : "Hesteharut" seul dit plus
        /// au joueur que "changer de posture", qui ne dit pas laquelle.
        /// </summary>
        private string TitleFor(AdvisorSuggestion suggestion)
        {
            switch (suggestion.kind)
            {
                case AdvisorActionKind.CreateTank:
                    return titleCreateTank;

                case AdvisorActionKind.ChangeStance:
                    return StanceName(suggestion.stance, titleChangeStance);

                case AdvisorActionKind.EvolveTank:
                    return titleEvolve;

                case AdvisorActionKind.Build:
                    if (suggestion.hex == null) return titleBuild;
                    if (suggestion.hex.type == TypeOfHex.hill) return titleBunker;
                    if (suggestion.hex.type == TypeOfHex.crystal) return titleCrystal;
                    if (suggestion.hex.type == TypeOfHex.mountain) return titleCommand;
                    return titleBuild;
            }

            return titleBuild;
        }

        private string StanceName(int stance, string fallback)
        {
            if (stanceNames == null || stance < 0 || stance >= stanceNames.Length) return fallback;

            string name = stanceNames[stance];
            return string.IsNullOrEmpty(name) ? fallback : name;
        }

        private string ReasonFor(AdvisorSuggestion suggestion)
        {
            if (reasons == null) return "";
            if (suggestion.reason < 0 || suggestion.reason >= reasons.Length) return "";
            return reasons[suggestion.reason];
        }

        private Color AccentFor(AdvisorSuggestion suggestion)
        {
            switch (suggestion.reason)
            {
                // La fenetre offensive et la Base en danger sont les deux urgences.
                case AdvisorRules.REASON_SHIELD_DOWN:
                case AdvisorRules.REASON_BASE_THREAT:
                case AdvisorRules.REASON_SURGE:
                case AdvisorRules.REASON_BRIDGEHEAD:

                // Un Shofar qui se refait efface l'avance deja gagnee : c'est une perte
                // en cours, pas une occasion manquee. Elle merite la meme alarme.
                case AdvisorRules.REASON_PORTAL_REGEN:
                    return urgentColor;
            }

            // Une action de posture porte la couleur de sa posture : le joueur relie
            // tout de suite la carte au Tank qu'il verra sur le plateau.
            if (suggestion.kind == AdvisorActionKind.ChangeStance
                || suggestion.kind == AdvisorActionKind.CreateTank)
                return MNLTHII.UI.StanceStyle.AccentOf(suggestion.stance);

            return calmColor;
        }

        // =================================================================
        //  APERCU
        // =================================================================
        private void Preview(AdvisorSuggestion suggestion)
        {
            if (suggestion == null) return;

            Vector3 from = WorldOf(suggestion.hex);
            Vector3 to = (suggestion.focus != null) ? WorldOf(suggestion.focus) : from;

            // Poser un batiment ne se "joue" pas : il n'y a pas de trajet a montrer,
            // seulement un endroit a regarder.
            bool isBuild = (suggestion.kind == AdvisorActionKind.Build);

            if (ghostOnHover && ThreatPreview.Instance != null && !isBuild)
            {
                bool attacks = (suggestion.focus != null)
                               && (suggestion.focus.type == TypeOfHex.portal)
                               && (to - from).sqrMagnitude < 0.01f;

                ThreatPreview.Instance.ShowHint(from, to, attacks,
                                                MNLTHII.UI.StanceStyle.AccentOf(suggestion.stance));
            }

            if (!focusOnHover || CameraDirector.Instance == null) return;

            // On cadre les DEUX bouts quand il y a un trajet : voir seulement le Tank
            // ne dit pas ou il va, et voir seulement le Shofar ne dit pas qui y va.
            if (!isBuild && (to - from).sqrMagnitude > 0.01f) CameraDirector.FrameAction(from, to);
            else CameraDirector.FocusPoint(from);
        }

        private void ClearPreview()
        {
            if (ThreatPreview.Instance != null) ThreatPreview.Instance.HideHint();
            CameraDirector.ReleaseCamera();
        }

        private static Vector3 WorldOf(Hexagon hex)
        {
            return (hex != null) ? hex.transform.position : Vector3.zero;
        }

        // =================================================================
        //  EXECUTION
        // =================================================================
        /// <summary>
        /// Le joueur accepte un conseil. L'action passe par les MEMES regles qu'un clic
        /// sur le plateau : le conseiller n'a aucun passe-droit, et il ne peut donc pas
        /// depenser une Energie que le joueur n'a pas.
        /// </summary>
        private void Apply(int slot)
        {
            if (slot < 0 || slot >= _count) return;

            AdvisorSuggestion suggestion = _suggestions[slot];
            if (suggestion == null) return;

            ClearPreview();
            _hovered = -1;

            TriviaOutcome outcome = TriviaOutcome.EnergyOnly;

            switch (suggestion.kind)
            {
                case AdvisorActionKind.CreateTank:
                    outcome = InteractionRules.CreateTankWithStance(suggestion.hex, (PawnStance)suggestion.stance);
                    break;

                case AdvisorActionKind.ChangeStance:
                    outcome = InteractionRules.ChangeTankStance(suggestion.tank, (PawnStance)suggestion.stance);
                    break;

                case AdvisorActionKind.EvolveTank:
                    outcome = InteractionRules.UpgradeTank(suggestion.tank);
                    break;

                case AdvisorActionKind.Build:
                    outcome = InteractionRules.ApplyPlayerAction(suggestion.hex);
                    break;
            }

            Debug.LogFormat("[Conseiller] {0} -> {1}.", suggestion.kind, outcome);

            if (TurnManager.Instance != null) TurnManager.Instance.NotifyAdvisorAction(outcome);
            else Refresh();
        }
    }
}
