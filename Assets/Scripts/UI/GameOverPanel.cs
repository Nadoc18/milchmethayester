using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using MNLTHII;
using MNLTHII.Rules;

namespace MNLTHII.Managers
{
    /// <summary>
    /// L'ecran de fin : victoire ou defaite.
    ///
    /// C'est le seul ecran du jeu qui doit CONCLURE. Les autres informent pendant que
    /// la partie continue ; celui-ci arrive quand tout est joue, et il a deux choses a
    /// faire, dans cet ordre :
    ///
    ///   1. DIRE LE RESULTAT, sans ambiguite et sans attendre. Un fondu lent sur une
    ///      victoire, c'est une seconde ou le joueur se demande encore s'il a gagne.
    ///
    ///   2. DIRE POURQUOI. Une fin sans bilan n'apprend rien : trois chiffres - les
    ///      tours tenus, les Shofars fermes, les ennemis abattus - transforment "j'ai
    ///      perdu" en "j'ai perdu au tour 14 avec deux Shofars encore ouverts", et
    ///      c'est cette phrase-la qui donne envie de rejouer.
    ///
    /// La victoire et la defaite partagent la meme mise en page. Seuls changent le
    /// titre, la couleur et la phrase. Deux ecrans differents auraient coute deux fois
    /// le travail pour rendre le jeu moins lisible, pas plus.
    ///
    /// Le bouton de reprise n'est cable a rien par defaut : rejouer veut dire
    /// rechargerla scene, et c'est une decision qui appartient au jeu, pas a un
    /// panneau d'interface. Branche-le sur ce que tu veux depuis l'inspecteur.
    ///
    /// Tous les libelles hebreux viennent de l'inspecteur, donc de hud_labels.json.
    /// Ce fichier .cs reste en pur ASCII.
    ///
    /// Note d'optimisation : aucun Update. Une seule coroutine, pour le fondu
    /// d'entree, et elle ne tourne qu'une fois par partie. Le fondu passe par un
    /// CanvasGroup - une seule valeur alpha - et non par la couleur de chaque texte,
    /// ce qui forcerait TextMeshPro a reconstruire son maillage a chaque frame.
    /// </summary>
    public class GameOverPanel : MonoBehaviour
    {
        public static GameOverPanel Instance;

        // =================================================================
        //  REFERENCES
        // =================================================================
        [Header("Racine")]
        public GameObject panelRoot;
        public CanvasGroup group;
        public Image dimmer;

        [Header("Entete")]
        public Image accentBar;
        public TMPro.TextMeshProUGUI titleText;
        public TMPro.TextMeshProUGUI subtitleText;

        [Header("Bilan")]
        public TMPro.TextMeshProUGUI[] statLabels = new TMPro.TextMeshProUGUI[3];
        public TMPro.TextMeshProUGUI[] statValues = new TMPro.TextMeshProUGUI[3];

        [Header("Reprise")]
        public Button replayButton;
        public TMPro.TextMeshProUGUI replayText;

        // =================================================================
        //  LIBELLES
        // =================================================================
        [Header("Libelles")]
        public string victoryTitle = "";
        public string victorySubtitle = "";
        public string defeatTitle = "";
        public string defeatSubtitle = "";
        public string replayLabel = "";

        public string statTurnsLabel = "";
        public string statPortalsLabel = "";
        public string statKillsLabel = "";

        public string portalsFormat = "{0}/{1}";

        // =================================================================
        //  COULEURS
        // =================================================================
        [Header("Couleurs")]
        public Color victoryColor = new Color(0.24f, 0.88f, 0.82f);
        public Color defeatColor = new Color(1f, 0.30f, 0.37f);

        [Header("Rythme")]
        public float fadeInDuration = 0.5f;

        private Coroutine _fade;
        private UnityEngine.Events.UnityAction _replayCallback;

        private void Awake()
        {
            if (Instance == null) Instance = this;

            // Le bouton "nouvelle partie" est cable ICI, au lancement. Le HudBuilder ne
            // peut pas le faire : un abonnement pose depuis un script d'editeur n'est
            // pas enregistre dans la scene - le bouton etait construit, visible, et muet.
            _replayCallback = Replay;
            if (replayButton != null) replayButton.onClick.AddListener(_replayCallback);

            if (panelRoot == null)
                Debug.LogWarning("[GameOver] panelRoot n'est pas renseigne : l'ecran de fin ne s'affichera pas.");

            if (group == null && panelRoot != null) group = panelRoot.GetComponent<CanvasGroup>();

            Hide();
        }

        private void OnDestroy()
        {
            if (replayButton != null && _replayCallback != null) replayButton.onClick.RemoveListener(_replayCallback);
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Nouvelle partie : on recharge la scene courante, exactement comme au
        /// lancement. La vitesse du jeu est remise a la normale d'abord - une partie
        /// terminee pendant un ralenti de la camera d'action ne doit pas en heriter.
        /// </summary>
        public void Replay()
        {
            Time.timeScale = 1f;

            // Le menu principal existe et fait partie du build : on y retourne, pour
            // pouvoir choisir une autre difficulte. Sinon, comme avant, on relance
            // directement la partie (avec la meme difficulte).
            if (Application.CanStreamedLevelBeLoaded(MainMenuController.MenuSceneName))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(MainMenuController.MenuSceneName);
                return;
            }

            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.name);
        }

        // =================================================================
        //  FACADE
        // =================================================================
        /// <summary>
        /// Appele par TurnManager a la fin de la partie. Tout autre etat est ignore :
        /// l'ecran ne doit s'ouvrir que sur une conclusion.
        /// </summary>
        public static void Report(StateOfGame state)
        {
            if (Instance == null) return;
            if (state != StateOfGame.victory && state != StateOfGame.defeat) return;

            Instance.Show(state == StateOfGame.victory);
        }

        public void Show(bool victory)
        {
            if (panelRoot == null) return;

            Color accent = victory ? victoryColor : defeatColor;

            if (accentBar != null) accentBar.color = accent;

            // LE RANG, SELON LE TANYA.
            //
            // Gagner ne dit pas COMMENT on a gagne. Abattre les six Shofars sans en
            // retourner un seul, c'est l'Atkafya pure : le Yetzer est soumis, il n'est
            // pas transforme. C'est la mesure du BEINONI - et ce n'est pas un echec,
            // le Tanya en fait le niveau de tout homme.
            //
            // En retourner une partie : TZADIK VE'RA LO, une part est devenue bien,
            // une part reste du mal tenu. Les retourner tous : TZADIK VE'TOV LO, il ne
            // reste plus rien a soumettre.
            //
            // Les trois titres vivent dans hud_labels.json. Aucun n'est une defaite.
            int turned = victory ? MNLTHII.Rules.InteractionRules.CountTurnedPortals() : 0;
            int fallen = victory ? MNLTHII.Rules.InteractionRules.CountFallenPortals() : 0;

            string rankTitle = null;
            string rankBody = null;

            if (victory && fallen > 0)
            {
                if (turned >= fallen)
                {
                    rankTitle = MNLTHII.UI.HudLabelsRuntime.Get("rankTzadikTov");
                    rankBody = MNLTHII.UI.HudLabelsRuntime.Get("rankTzadikTovBody");
                }
                else if (turned > 0)
                {
                    rankTitle = MNLTHII.UI.HudLabelsRuntime.Get("rankTzadikRa");
                    rankBody = MNLTHII.UI.HudLabelsRuntime.Get("rankTzadikRaBody");
                }
                else
                {
                    rankTitle = MNLTHII.UI.HudLabelsRuntime.Get("rankBeinoni");
                    rankBody = MNLTHII.UI.HudLabelsRuntime.Get("rankBeinoniBody");
                }
            }

            if (titleText != null)
            {
                titleText.text = string.IsNullOrEmpty(rankTitle)
                                 ? (victory ? victoryTitle : defeatTitle)
                                 : rankTitle;
                titleText.color = accent;
            }

            if (subtitleText != null)
            {
                subtitleText.text = string.IsNullOrEmpty(rankBody)
                                    ? (victory ? victorySubtitle : defeatSubtitle)
                                    : rankBody;
            }

            FillStats(accent);

            if (replayText != null) replayText.text = replayLabel;

            panelRoot.SetActive(true);

            // Si la partie s'est terminee pendant une vue immersive, le HUD parent a pu
            // etre masque (alpha 0, clics bloques) : l'ecran de fin serait alors
            // invisible ou son bouton inerte. On rend la main a toute la chaine.
            Transform parent = panelRoot.transform.parent;
            while (parent != null)
            {
                CanvasGroup g = parent.GetComponent<CanvasGroup>();
                if (g != null)
                {
                    g.alpha = 1f;
                    g.blocksRaycasts = true;
                    g.interactable = true;
                }
                parent = parent.parent;
            }

            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadeIn());
        }

        public void Hide()
        {
            if (group != null) group.alpha = 0f;
            if (panelRoot != null && panelRoot.activeSelf) panelRoot.SetActive(false);
        }

        // =================================================================
        //  BILAN
        // =================================================================
        /// <summary>
        /// Les trois chiffres qui resument la partie. Ils sont lus a la source au
        /// moment de l'affichage : rien n'est accumule pendant la partie, donc rien ne
        /// peut se desynchroniser.
        /// </summary>
        private void FillStats(Color accent)
        {
            TurnManager turns = TurnManager.Instance;
            PortalManager portals = PortalManager.Instance;

            int turnCount = (turns != null) ? turns.currentTurn : 0;

            // Shofars fermes = ceux du depart moins ceux qui tiennent encore.
            int remaining = (portals != null) ? portals.CountPortals() : 0;
            int total = InteractionRules.PORTAL_COUNT;
            int closed = total - remaining;
            if (closed < 0) closed = 0;

            int kills = (portals != null) ? portals.totalEnemiesKilled : 0;

            SetStat(0, statTurnsLabel, turnCount, accent);
            SetStatFormatted(1, statPortalsLabel, portalsFormat, closed, total, accent);
            SetStat(2, statKillsLabel, kills, accent);
        }

        private void SetStat(int index, string label, int value, Color tint)
        {
            if (statLabels != null && index < statLabels.Length && statLabels[index] != null)
                statLabels[index].text = label;

            if (statValues != null && index < statValues.Length && statValues[index] != null)
            {
                statValues[index].SetText("{0}", value);
                statValues[index].color = tint;
            }
        }

        private void SetStatFormatted(int index, string label, string format, int a, int b, Color tint)
        {
            if (statLabels != null && index < statLabels.Length && statLabels[index] != null)
                statLabels[index].text = label;

            if (statValues != null && index < statValues.Length && statValues[index] != null)
            {
                statValues[index].SetText(format, a, b);
                statValues[index].color = tint;
            }
        }

        // =================================================================
        //  FONDU
        // =================================================================
        private IEnumerator FadeIn()
        {
            if (group == null) yield break;

            if (fadeInDuration <= 0f) { group.alpha = 1f; _fade = null; yield break; }

            float elapsed = 0f;
            group.alpha = 0f;

            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                float t = elapsed / fadeInDuration;
                if (t > 1f) t = 1f;

                group.alpha = t * t * (3f - 2f * t);
                yield return null;
            }

            group.alpha = 1f;
            _fade = null;
        }
    }
}
