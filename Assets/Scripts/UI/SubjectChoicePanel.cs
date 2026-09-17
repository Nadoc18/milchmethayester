using System;
using UnityEngine;
using UnityEngine.UI;

namespace MNLTHII.Managers
{
    /// <summary>
    /// Le choix du sujet - la premiere vraie decision de chaque tour.
    ///
    /// LA MECANIQUE, ET POURQUOI ELLE TIENT
    ///
    /// Quatre sujets, quatre gains differents, qui changent a chaque tour. Le gain
    /// affiche n'est pas decoratif : c'est le palier de difficulte de la VRAIE
    /// question deja tiree pour ce sujet. Voir +85 sur Moussar, c'est savoir qu'on
    /// prend la question la plus dure du tour.
    ///
    /// Les quatre gains sont calibres pour rapporter a peu pres la meme chose EN
    /// MOYENNE - voir les paliers dans InteractionRules. Ce n'est pas un detail
    /// d'equilibrage, c'est ce qui fait exister le choix : si le sujet difficile
    /// rapportait plus en esperance, il n'y aurait rien a decider, on prendrait le
    /// plus cher a chaque tour et l'ecran deviendrait un clic de plus.
    ///
    /// Ce qui se decide vraiment ici, c'est le RISQUE :
    ///
    ///   - a 30 d'Energie et un Tank a sortir absolument ce tour-ci, le +25 sur
    ///     lequel on peut compter vaut mieux qu'un +85 qui a deux chances sur trois
    ///     de ne rien donner ;
    ///   - quand un Shofar est sur le point de tomber et qu'il manque 60, le pari
    ///     devient la seule ligne qui mene quelque part.
    ///
    /// Et il reste la connaissance : un joueur fort en Halacha prendra le +85 quand
    /// il tombe sur son domaine, et le +25 quand il tombe ailleurs. Le savoir ne
    /// remplace pas la strategie, il en change le prix.
    ///
    /// Ce composant ne cree rien et ne decide rien : il affiche l'offre du tour et
    /// signale le sujet choisi. Les libelles hebreux viennent de l'inspecteur, donc
    /// de hud_labels.json, et ce .cs reste en pur ASCII.
    ///
    /// Note d'optimisation : les quatre delegues de bouton sont construits une fois
    /// dans Awake et jamais recrees - un AddListener(() => X(i)) dans une boucle
    /// allouerait une closure par tour. Les nombres passent par SetText(format,
    /// valeur), qui n'alloue pas de string.
    /// </summary>
    public class SubjectChoicePanel : MonoBehaviour
    {
        public static SubjectChoicePanel Instance;

        /// <summary>Quatre sujets : Halacha, Tanakh, Moussar, Tefila.</summary>
        public const int SlotCount = 4;

        // =================================================================
        //  REFERENCES
        // =================================================================
        [Header("Racine")]
        public GameObject panelRoot;

        [Header("Entete")]
        public TMPro.TextMeshProUGUI titleText;
        public TMPro.TextMeshProUGUI subtitleText;

        [Header("Les quatre cartes")]
        public Button[] cardButtons = new Button[SlotCount];
        public Image[] cardBackgrounds = new Image[SlotCount];
        public Image[] cardAccents = new Image[SlotCount];
        public TMPro.TextMeshProUGUI[] cardNames = new TMPro.TextMeshProUGUI[SlotCount];
        public TMPro.TextMeshProUGUI[] cardRewards = new TMPro.TextMeshProUGUI[SlotCount];
        public TMPro.TextMeshProUGUI[] cardDifficultyLabels = new TMPro.TextMeshProUGUI[SlotCount];

        /// <summary>Quatre pastilles par carte : le palier de difficulte, en clair.</summary>
        public Image[] cardDots = new Image[SlotCount * 4];

        // =================================================================
        //  LIBELLES
        // =================================================================
        [Header("Libelles")]
        public string title = "";
        public string subtitle = "";
        public string rewardFormat = "+{0}";

        // Un nom par palier : facile, moyen, difficile, tres difficile.
        public string[] difficultyNames = new string[4];

        // Traduction des sujets, comme dans TriviaPanelController : deux tableaux
        // paralleles. Le JSON des questions stocke la categorie en latin ; l'afficher
        // telle quelle dans un champ droite-a-gauche la retournerait a l'ecran.
        public string[] categoryKeys = new string[0];
        public string[] categoryNames = new string[0];

        // =================================================================
        //  COULEURS
        // =================================================================
        [Header("Couleurs")]
        public Color cardIdleColor = new Color(0.49f, 0.63f, 1f, 0.06f);
        public Color cardHoverColor = new Color(0.24f, 0.88f, 0.82f, 0.16f);
        public Color dotOffColor = new Color(0.49f, 0.63f, 1f, 0.15f);

        /// <summary>
        /// Une couleur par palier. Le degrade va du cyan tranquille au magenta : le
        /// joueur doit sentir le risque avant meme d'avoir lu le chiffre.
        /// </summary>
        public Color[] difficultyColors = new Color[4]
        {
            new Color(0.24f, 0.88f, 0.82f),   // 1 - sur
            new Color(0.55f, 0.82f, 0.55f),   // 2
            new Color(1f, 0.78f, 0.36f),      // 3
            new Color(1f, 0.24f, 0.60f)       // 4 - le pari
        };

        // =================================================================
        //  ETAT INTERNE
        // =================================================================
        private Action<int> _handler;
        private UnityEngine.Events.UnityAction[] _callbacks;
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
            if (cardButtons == null) return;

            _callbacks = new UnityEngine.Events.UnityAction[cardButtons.Length];

            for (int i = 0; i < cardButtons.Length; i++)
            {
                if (cardButtons[i] == null) continue;

                int slot = i;                       // capture unique, a la construction
                _callbacks[i] = delegate { Choose(slot); };
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
        public void SetChoiceHandler(Action<int> handler)
        {
            _handler = handler;
        }

        /// <summary>
        /// Ouvre l'ecran avec l'offre du tour. Les trois tableaux sont paralleles et
        /// dans l'ordre des sujets. keys porte les cles LATINES du JSON des questions,
        /// traduites ici : une cle vide signifie qu'il ne restait plus de question pour
        /// ce sujet, et sa carte est desactivee plutot que d'afficher un gain qu'on ne
        /// pourrait pas tenir.
        /// </summary>
        public void Show(string[] keys, int[] rewards, int[] difficulties)
        {
            _locked = false;

            if (titleText != null) titleText.text = title;
            if (subtitleText != null) subtitleText.text = subtitle;

            for (int i = 0; i < SlotCount; i++)
            {
                bool usable = keys != null && i < keys.Length && !string.IsNullOrEmpty(keys[i]);

                if (cardButtons != null && i < cardButtons.Length && cardButtons[i] != null)
                {
                    cardButtons[i].gameObject.SetActive(usable);
                    cardButtons[i].interactable = usable;
                }

                if (!usable) continue;

                int tier = (difficulties != null && i < difficulties.Length) ? difficulties[i] : 1;
                if (tier < 1) tier = 1;
                if (tier > 4) tier = 4;

                Color tone = difficultyColors[tier - 1];

                if (cardNames != null && i < cardNames.Length && cardNames[i] != null)
                    cardNames[i].text = TranslateCategory(keys[i]);

                if (cardRewards != null && i < cardRewards.Length && cardRewards[i] != null)
                {
                    int reward = (rewards != null && i < rewards.Length) ? rewards[i] : 0;
                    cardRewards[i].SetText(rewardFormat, reward);
                    cardRewards[i].color = tone;
                }

                if (cardDifficultyLabels != null && i < cardDifficultyLabels.Length
                    && cardDifficultyLabels[i] != null)
                {
                    cardDifficultyLabels[i].text = (difficultyNames != null && tier - 1 < difficultyNames.Length)
                                                   ? difficultyNames[tier - 1] : "";
                    cardDifficultyLabels[i].color = tone;
                }

                if (cardAccents != null && i < cardAccents.Length && cardAccents[i] != null)
                    cardAccents[i].color = tone;

                if (cardBackgrounds != null && i < cardBackgrounds.Length && cardBackgrounds[i] != null)
                    cardBackgrounds[i].color = cardIdleColor;

                SetDots(i, tier, tone);
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
        /// <summary>
        /// Les quatre pastilles d'une carte. Le chiffre du gain dit combien ; les
        /// pastilles disent pourquoi. Sans elles, le joueur croit a un tirage au sort
        /// alors que c'est une echelle de difficulte.
        /// </summary>
        private void SetDots(int slot, int tier, Color tone)
        {
            if (cardDots == null) return;

            int first = slot * 4;

            for (int d = 0; d < 4; d++)
            {
                int index = first + d;
                if (index >= cardDots.Length) return;

                Image dot = cardDots[index];
                if (dot == null) continue;

                dot.color = (d < tier) ? tone : dotOffColor;
            }
        }

        /// <summary>
        /// Cle latine du JSON vers le nom hebreu. Cle inconnue : on renvoie une chaine
        /// vide plutot que le latin. Mieux vaut un titre absent qu'un titre imprime a
        /// l'envers - c'est exactement le bug "AHCALAH" vu en jeu.
        /// </summary>
        private string TranslateCategory(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            if (categoryKeys == null || categoryNames == null) return "";

            for (int i = 0; i < categoryKeys.Length && i < categoryNames.Length; i++)
                if (categoryKeys[i] == key) return categoryNames[i];

            return "";
        }

        private void Choose(int slot)
        {
            if (_locked) return;
            _locked = true;

            if (_handler != null) _handler(slot);
        }
    }
}
