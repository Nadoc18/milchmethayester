using System;
using UnityEngine;
using UnityEngine.UI;

namespace MNLTHII.Managers
{
    /// <summary>
    /// Ce qu'une carte de l'ecran propose de faire.
    /// </summary>
    public enum TankChoiceKind
    {
        /// <summary>Creer un Tank neuf, deja dans la posture choisie.</summary>
        Create,

        /// <summary>Changer la posture d'un Tank existant, contre de l'Energie.</summary>
        Restance,

        /// <summary>Faire passer ce Tank au Niveau 2.</summary>
        Evolve
    }

    /// <summary>
    /// L'ecran de choix d'un Tank : sa posture a la creation, ou ce qu'on fait d'un
    /// Tank deja pose.
    ///
    /// POURQUOI IL REMPLACE LE CLIC QUI FAISAIT TOURNER LA POSTURE
    ///
    /// L'ancien systeme cyclait Garde puis Assaut puis Chasse a chaque clic. Trois
    /// defauts, qui se cumulaient :
    ///
    ///   - le joueur ne savait pas ce qu'il allait obtenir avant de cliquer ;
    ///   - il ne savait pas non plus ce que chaque posture FAIT, donc le cycle ne
    ///     l'aidait pas ;
    ///   - et un Tank pres d'un Cristal partait en evolution au lieu de changer de
    ///     posture, sans que rien ne l'annonce.
    ///
    /// Un ecran qui montre les trois cote a cote, chacun avec sa phrase et son prix,
    /// regle les trois d'un coup. Et il rassemble au meme endroit TOUT ce qu'on peut
    /// faire a un Tank : l'evolution devient une quatrieme carte au lieu d'une action
    /// concurrente qui volait le clic.
    ///
    /// LE PRIX D'UN CHANGEMENT
    ///
    /// Changer la posture d'un Tank deja pose coute de l'Energie ; la choisir a la
    /// creation est gratuit, puisqu'on paie deja le Tank. Sans ce cout, la posture ne
    /// serait pas une decision : on ajusterait les six Tanks a chaque tour selon la
    /// menace, et l'engagement - "ce Tank-la tient la ligne" - n'existerait plus.
    ///
    /// Ce composant ne cree rien et ne decide rien. Il affiche ce qu'on lui donne et
    /// signale l'index choisi ; c'est TurnManager qui applique et qui facture. Les
    /// libelles hebreux viennent de l'inspecteur, donc de hud_labels.json.
    ///
    /// Note d'optimisation : les delegues de bouton sont construits une fois dans
    /// Awake. Les tableaux d'options sont fournis par l'appelant et reutilises d'un
    /// clic a l'autre, donc rien n'est alloue a l'ouverture.
    /// </summary>
    public class TankChoicePanel : MonoBehaviour
    {
        public static TankChoicePanel Instance;

        /// <summary>Trois postures, plus une carte d'evolution eventuelle.</summary>
        public const int MaxOptions = 4;

        // =================================================================
        //  REFERENCES
        // =================================================================
        [Header("Racine")]
        public GameObject panelRoot;

        [Header("Entete")]
        public TMPro.TextMeshProUGUI titleText;
        public TMPro.TextMeshProUGUI subtitleText;

        [Header("Les cartes")]
        public Button[] cardButtons = new Button[MaxOptions];
        public Image[] cardBackgrounds = new Image[MaxOptions];
        public Image[] cardAccents = new Image[MaxOptions];

        /// <summary>
        /// Le pictogramme de la carte, teinte par la couleur de la posture. Trois
        /// chevrons de couleurs differentes se distinguent plus vite que trois mots
        /// hebreux, surtout quand on joue vite.
        /// </summary>
        public Image[] cardIcons = new Image[MaxOptions];
        public TMPro.TextMeshProUGUI[] cardNames = new TMPro.TextMeshProUGUI[MaxOptions];
        public TMPro.TextMeshProUGUI[] cardBodies = new TMPro.TextMeshProUGUI[MaxOptions];
        public TMPro.TextMeshProUGUI[] cardCosts = new TMPro.TextMeshProUGUI[MaxOptions];

        [Header("Annuler")]
        public Button cancelButton;
        public TMPro.TextMeshProUGUI cancelText;

        // =================================================================
        //  LIBELLES
        // =================================================================
        [Header("Libelles")]
        public string createTitle = "";
        public string createSubtitle = "";
        public string changeTitle = "";
        public string changeSubtitle = "";
        public string cancelLabel = "";
        public string freeLabel = "";
        public string currentLabel = "";
        public string evolveName = "";
        public string evolveBody = "";

        // Garde, Assaut, Chasse - dans l'ordre de l'enum PawnStance.
        public string[] stanceNames = new string[3];
        public string[] stanceBodies = new string[3];

        // =================================================================
        //  COULEURS
        // =================================================================
        [Header("Couleurs")]
        public Color cardIdleColor = new Color(0.49f, 0.63f, 1f, 0.06f);
        public Color cardHoverColor = new Color(0.49f, 0.63f, 1f, 0.16f);
        public Color affordableColor = new Color(1f, 0.78f, 0.36f);
        public Color tooExpensiveColor = new Color(1f, 0.30f, 0.37f);
        public Color freeColor = new Color(0.24f, 0.88f, 0.82f);
        public Color currentColor = new Color(0.56f, 0.64f, 0.80f);
        public Color evolveColor = new Color(0.65f, 0.42f, 1f);

        // =================================================================
        //  ETAT INTERNE
        // =================================================================
        private Action<int> _handler;
        private UnityEngine.Events.UnityAction[] _callbacks;
        private UnityEngine.Events.UnityAction _cancelCallback;
        private bool _locked;

        public bool IsOpen { get { return panelRoot != null && panelRoot.activeSelf; } }

        private void Awake()
        {
            if (Instance == null) Instance = this;

            BindButtons();
            Hide();
        }

        private void BindButtons()
        {
            if (cardButtons != null)
            {
                _callbacks = new UnityEngine.Events.UnityAction[cardButtons.Length];

                for (int i = 0; i < cardButtons.Length; i++)
                {
                    if (cardButtons[i] == null) continue;

                    int slot = i;                  // capture unique, a la construction
                    _callbacks[i] = delegate { Choose(slot); };
                    cardButtons[i].onClick.AddListener(_callbacks[i]);
                }
            }

            if (cancelButton != null)
            {
                _cancelCallback = delegate { Choose(-1); };
                cancelButton.onClick.AddListener(_cancelCallback);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (cardButtons != null && _callbacks != null)
            {
                for (int i = 0; i < cardButtons.Length && i < _callbacks.Length; i++)
                    if (cardButtons[i] != null && _callbacks[i] != null)
                        cardButtons[i].onClick.RemoveListener(_callbacks[i]);
            }

            if (cancelButton != null && _cancelCallback != null)
                cancelButton.onClick.RemoveListener(_cancelCallback);
        }

        // =================================================================
        //  API
        // =================================================================
        /// <summary>
        /// Qui recoit l'index choisi : 0 a 2 pour une posture, 3 pour l'evolution,
        /// -1 pour une annulation.
        /// </summary>
        public void SetChoiceHandler(Action<int> handler)
        {
            _handler = handler;
        }

        /// <summary>
        /// Ouvre l'ecran.
        ///
        /// currentStance vaut -1 a la creation - aucune posture n'est encore "celle
        /// du Tank". Sinon la carte correspondante est marquee et desactivee : payer
        /// pour rester dans la meme posture n'est pas un choix, c'est un piege.
        ///
        /// evolveCost vaut 0 quand l'evolution est impossible ; la quatrieme carte
        /// est alors masquee plutot que grisee. Une case grise sans explication
        /// occupe de la place et ne dit rien.
        ///
        /// stanceCost est le prix affiche sur les trois cartes de posture : le prix du
        /// Tank a la creation, le prix du changement ensuite.
        /// </summary>
        public void Show(bool creating, int stanceCost, int currentStance,
                         int evolveCost, int availableEnergy)
        {
            _locked = false;

            if (titleText != null) titleText.text = creating ? createTitle : changeTitle;
            if (subtitleText != null) subtitleText.text = creating ? createSubtitle : changeSubtitle;
            if (cancelText != null) cancelText.text = cancelLabel;

            for (int i = 0; i < StanceCount(); i++)
            {
                bool isCurrent = (i == currentStance);

                // Le prix vient de l'appelant, sans exception : a la creation c'est le
                // prix du Tank, ensuite celui du changement de posture. L'afficher
                // "gratuit" a la creation serait un mensonge - le Tank coute 50.
                bool affordable = !isCurrent && (stanceCost <= 0 || availableEnergy >= stanceCost);

                FillCard(i, UI.StanceStyle.AccentOf(i),
                         NameAt(stanceNames, i), NameAt(stanceBodies, i),
                         stanceCost, affordable, isCurrent, true);
            }

            // --- la carte d'evolution ---
            bool showEvolve = !creating && evolveCost > 0;

            if (cardButtons != null && 3 < cardButtons.Length && cardButtons[3] != null)
                cardButtons[3].gameObject.SetActive(showEvolve);

            if (showEvolve)
            {
                FillCard(3, evolveColor, evolveName, evolveBody,
                         evolveCost, availableEnergy >= evolveCost, false, true);
            }

            if (panelRoot != null) panelRoot.SetActive(true);
        }

        public void Hide()
        {
            _locked = false;
            if (panelRoot != null && panelRoot.activeSelf) panelRoot.SetActive(false);
        }

        /// <summary>Survol : appele par les relais poses sur chaque carte.</summary>
        public void SetHovered(int slot, bool hovered)
        {
            if (_locked) return;
            if (cardBackgrounds == null || slot < 0 || slot >= cardBackgrounds.Length) return;

            Image bg = cardBackgrounds[slot];
            if (bg == null) return;

            bg.color = hovered ? cardHoverColor : cardIdleColor;
        }

        // =================================================================
        //  INTERNE
        // =================================================================
        private int StanceCount()
        {
            int limit = UI.StanceStyle.Count;
            if (cardButtons != null && cardButtons.Length < limit) limit = cardButtons.Length;
            return limit;
        }

        private static string NameAt(string[] table, int index)
        {
            if (table == null || index < 0 || index >= table.Length) return "";
            return table[index];
        }

        private void FillCard(int slot, Color accent, string name, string body,
                              int cost, bool affordable, bool isCurrent, bool visible)
        {
            if (cardButtons != null && slot < cardButtons.Length && cardButtons[slot] != null)
            {
                cardButtons[slot].gameObject.SetActive(visible);

                // Une carte inabordable reste LISIBLE mais ne se clique pas. La
                // masquer priverait le joueur de l'information la plus utile :
                // combien il lui manque.
                cardButtons[slot].interactable = affordable;
            }

            if (cardAccents != null && slot < cardAccents.Length && cardAccents[slot] != null)
                cardAccents[slot].color = accent;

            if (cardIcons != null && slot < cardIcons.Length && cardIcons[slot] != null)
                cardIcons[slot].color = accent;

            if (cardBackgrounds != null && slot < cardBackgrounds.Length && cardBackgrounds[slot] != null)
                cardBackgrounds[slot].color = cardIdleColor;

            if (cardNames != null && slot < cardNames.Length && cardNames[slot] != null)
            {
                cardNames[slot].text = name;
                cardNames[slot].color = accent;
            }

            if (cardBodies != null && slot < cardBodies.Length && cardBodies[slot] != null)
                cardBodies[slot].text = body;

            if (cardCosts == null || slot >= cardCosts.Length || cardCosts[slot] == null) return;

            TMPro.TextMeshProUGUI costText = cardCosts[slot];

            if (isCurrent)
            {
                costText.text = currentLabel;
                costText.color = currentColor;
            }
            else if (cost <= 0)
            {
                costText.text = freeLabel;
                costText.color = freeColor;
            }
            else
            {
                costText.SetText("{0}", cost);
                costText.color = affordable ? affordableColor : tooExpensiveColor;
            }
        }

        private void Choose(int slot)
        {
            if (_locked) return;
            _locked = true;

            Hide();

            if (_handler != null) _handler(slot);
        }
    }
}
