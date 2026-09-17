using UnityEngine;
using UnityEngine.UI;
using MNLTHII;
using MNLTHII.Rules;

namespace MNLTHII.Managers
{
    /// <summary>
    /// Le panneau d'etat d'un Shofar, au survol, pendant la phase de depense.
    ///
    /// Pourquoi il existe : l'instabilite des portails est LE mecanisme central du
    /// jeu - chaque ennemi tue deleste le portail qui l'a envoye, quatre morts
    /// rompent son bouclier pendant quatre tours, et c'est la seule fenetre ou un
    /// Shofar se ferme en un coup. Tout cela se joue aujourd'hui entierement hors
    /// ecran : le joueur subit une boucle qu'il ne voit pas. Sans ce panneau, le
    /// coeur strategique du jeu reste invisible.
    ///
    /// Comme HudController, ce composant ne CREE rien. Tu construis le panneau (ou
    /// tu laisses Milchemet > Construire le HUD le faire), tu glisses les
    /// references, il se contente de les remplir.
    ///
    /// Aucun texte en dur ici : tous les libelles hebreux sont des champs de
    /// l'inspecteur, alimentes par Assets/Resources/hud_labels.json. Ce fichier .cs
    /// reste en pur ASCII et survit a n'importe quel re-enregistrement.
    ///
    /// Note d'optimisation : aucune allocation par frame. Pas de raycast propre -
    /// on relit l'hexagone deja trouve par HexTooltipController. Le remplissage
    /// n'a lieu que si quelque chose a REELLEMENT change (portail survole, PV,
    /// morts portees au compte, tours de bouclier restants) : compare a quatre int,
    /// c'est gratuit, et le reste du temps Update sort en trois lignes.
    /// SetText(format, arg) de TextMeshPro ecrit dans son propre tampon de char :
    /// pas de string.Format, donc pas un octet de GC.
    /// </summary>
    public class PortalPanelController : MonoBehaviour
    {
        // =================================================================
        //  REFERENCES - a glisser dans l'inspecteur
        // =================================================================
        [Header("Racine")]
        public GameObject panelRoot;

        [Header("Entete")]
        public Image accentBar;
        public TMPro.TextMeshProUGUI titleText;
        public TMPro.TextMeshProUGUI levelText;

        [Header("Points de vie")]
        public TMPro.TextMeshProUGUI hpText;
        public Image hpFill;

        [Header("Instabilite")]
        public TMPro.TextMeshProUGUI instabilityText;
        public Image[] instabilitySegments = new Image[4];

        [Header("Bouclier")]
        public Image shieldBox;
        public TMPro.TextMeshProUGUI shieldTitleText;
        public TMPro.TextMeshProUGUI shieldBodyText;

        [Header("Evolution")]
        public GameObject evolveRow;
        public TMPro.TextMeshProUGUI evolveText;

        // =================================================================
        //  LIBELLES - remplis depuis le JSON par le constructeur de HUD
        // =================================================================
        [Header("Libelles")]
        // Six noms, dans l'ordre : est, nord-est, nord-ouest, ouest, sud-ouest, sud-est.
        public string[] directionNames = new string[6];

        public string levelFormat = "{0}";
        public string instabilityFormat = "{0}/{1}";
        public string hpFormat = "{0}/{1}";
        public string evolveFormat = "{0}";

        public string shieldIntactTitle = "";
        public string shieldIntactBody = "";
        public string shieldBrokenTitle = "";
        public string shieldBrokenBodyFormat = "{0}";
        public string surgeTitle = "";
        public string surgeBodyFormat = "{0}";

        // =================================================================
        //  COULEURS
        // =================================================================
        [Header("Couleurs")]
        public Color portalColor = new Color(1f, 0.42f, 0.29f);          // orange
        public Color surgeColor = new Color(1f, 0.24f, 0.60f);           // magenta
        public Color shieldDownColor = new Color(1f, 0.42f, 0.69f);
        public Color neutralColor = new Color(0.56f, 0.64f, 0.80f);
        public Color segmentOffColor = new Color(0.49f, 0.63f, 1f, 0.15f);
        public Color boxNeutralColor = new Color(0.49f, 0.63f, 1f, 0.05f);
        public Color boxAlertColor = new Color(1f, 0.24f, 0.60f, 0.10f);

        [Header("Rythme")]
        [Tooltip("Secondes entre deux relectures. 0.15 = imperceptible, et six fois moins de travail qu'a chaque frame.")]
        public float refreshInterval = 0.15f;

        // =================================================================
        //  ETAT INTERNE
        // =================================================================
        private Hexagon _current;
        private float _nextRefresh;

        // Detection de changement : on ne reecrit un texte que si sa source a bouge.
        private int _lastHp = -1;
        private int _lastMaxHp = -1;
        private int _lastKills = -1;
        private int _lastShield = -1;
        private int _lastLevel = -1;
        private bool _lastSurge;

        /// <summary>
        /// Le plus haut niveau annonce. Memorise comme le reste : sans lui, l'encadre
        /// ne serait pas redessine si seul le niveau changeait d'un tour a l'autre.
        /// </summary>
        private int _lastTopLevel = -1;

        private void Awake()
        {
            // Volontairement PAS de repli sur gameObject : ce composant vit sur le
            // Canvas, et un SetActive(false) dessus eteindrait toute l'interface.
            // Sans reference, le panneau reste simplement muet.
            if (panelRoot == null)
                Debug.LogWarning("[PortalPanel] panelRoot n'est pas renseigne : le panneau ne s'affichera pas.");

            Hide();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + refreshInterval;

            // Hors phase de depense, le panneau n'a rien a dire : le joueur ne peut
            // de toute facon rien decider a ce moment-la.
            TurnManager turns = TurnManager.Instance;
            if (turns == null || !turns.IsSpendingPhase) { Clear(); return; }

            // On ne relance pas de raycast : l'info-bulle en fait deja un, vingt fois
            // par seconde. Deux sondages pour la meme case seraient du gaspillage.
            HexTooltipController probe = HexTooltipController.Instance;
            if (probe == null) { Clear(); return; }

            Hexagon hex = probe.HoveredHex;
            if (hex == null || hex.type != TypeOfHex.portal) { Clear(); return; }

            if (hex != _current)
            {
                _current = hex;
                InvalidateCache();
                FillTitle(hex);
            }

            FillState(hex);
        }

        // =================================================================
        //  AFFICHAGE
        // =================================================================
        /// <summary>Entete : nom du Shofar et niveau. Ne change qu'au changement de case.</summary>
        private void FillTitle(Hexagon portal)
        {
            if (panelRoot != null && !panelRoot.activeSelf) panelRoot.SetActive(true);

            if (titleText != null)
            {
                int dir = DirectionIndex(portal);
                titleText.text = (directionNames != null && dir >= 0 && dir < directionNames.Length)
                                 ? directionNames[dir]
                                 : "";
            }
        }

        /// <summary>Corps : PV, instabilite, bouclier, evolution. Seul ce qui bouge est reecrit.</summary>
        private void FillState(Hexagon portal)
        {
            PortalManager portals = PortalManager.Instance;

            int hp = portal.currentHP;
            int maxHp = portal.maxHP;
            if (maxHp <= 0) maxHp = InteractionRules.GetBuildingMaxHP(TypeOfHex.portal, portal.level);

            int kills = (portals != null) ? portals.GetInstability(portal) : 0;
            int shield = (portals != null) ? portals.GetShieldDownTurns(portal) : 0;
            int level = portal.level;
            bool surge = (portals != null) && portals.IsSurgeAnnounced(portal);

            // --- niveau ---
            if (level != _lastLevel)
            {
                _lastLevel = level;
                if (levelText != null) levelText.SetText(levelFormat, level);
            }

            // --- points de vie ---
            if (hp != _lastHp || maxHp != _lastMaxHp)
            {
                _lastHp = hp;
                _lastMaxHp = maxHp;

                if (hpText != null) hpText.SetText(hpFormat, hp, maxHp);
                if (hpFill != null)
                    hpFill.fillAmount = (maxHp > 0) ? Mathf.Clamp01((float)hp / maxHp) : 0f;
            }

            // --- instabilite ---
            if (kills != _lastKills)
            {
                _lastKills = kills;

                int needed = InteractionRules.PORTAL_KILLS_TO_BREAK_SHIELD;
                int shown = (kills > needed) ? needed : kills;

                if (instabilityText != null) instabilityText.SetText(instabilityFormat, shown, needed);

                if (instabilitySegments != null)
                {
                    for (int i = 0; i < instabilitySegments.Length; i++)
                    {
                        Image seg = instabilitySegments[i];
                        if (seg == null) continue;
                        seg.color = (i < shown) ? portalColor : segmentOffColor;
                    }
                }
            }

            // --- bouclier et vague annoncee ---
            int topLevel = (portals != null && surge) ? portals.PredictHighestLevel(portal) : 0;

            if (shield != _lastShield || surge != _lastSurge || topLevel != _lastTopLevel)
            {
                _lastShield = shield;
                _lastSurge = surge;
                _lastTopLevel = topLevel;
                FillShield(portal, shield, surge);
            }

            // --- evolution : recalcule a chaque passage, c'est un seul int ---
            int untilEvolve = (portals != null) ? portals.GetTurnsUntilEvolve(portal) : -1;

            if (evolveRow != null) evolveRow.SetActive(untilEvolve >= 0);
            if (untilEvolve >= 0 && evolveText != null) evolveText.SetText(evolveFormat, untilEvolve);
        }

        /// <summary>
        /// L'encadre du bas. Trois etats, par ordre d'urgence :
        /// bouclier rompu (la fenetre a saisir), vague annoncee, bouclier intact.
        /// </summary>
        private void FillShield(Hexagon portal, int shieldTurns, bool surge)
        {
            Color accent = portalColor;

            if (shieldTurns > 0)
            {
                accent = shieldDownColor;

                if (shieldTitleText != null)
                {
                    shieldTitleText.text = shieldBrokenTitle;
                    shieldTitleText.color = shieldDownColor;
                }
                if (shieldBodyText != null)
                    shieldBodyText.SetText(shieldBrokenBodyFormat, shieldTurns);

                if (shieldBox != null) shieldBox.color = boxAlertColor;
            }
            else if (surge)
            {
                accent = surgeColor;

                if (shieldTitleText != null)
                {
                    shieldTitleText.text = surgeTitle;
                    shieldTitleText.color = surgeColor;
                }
                if (shieldBodyText != null)
                {
                    // Le nombre ET le niveau viennent du plan reellement tire par
                    // PortalManager, pas d'une constante : trois Niveau 1 et trois
                    // Niveau 3 ne se preparent pas de la meme facon, et afficher le
                    // meme texte pour les deux revenait a ne rien dire.
                    PortalManager source = PortalManager.Instance;

                    int count = (source != null) ? source.PredictSpawnCount(portal) : 0;
                    if (count <= 0) count = 1 + InteractionRules.SURGE_EXTRA_SPAWNS;

                    int top = (source != null) ? source.PredictHighestLevel(portal) : 0;
                    if (top < 1) top = 1;

                    shieldBodyText.SetText(surgeBodyFormat, count, top);
                }

                if (shieldBox != null) shieldBox.color = boxAlertColor;
            }
            else
            {
                if (shieldTitleText != null)
                {
                    shieldTitleText.text = shieldIntactTitle;
                    shieldTitleText.color = neutralColor;
                }
                if (shieldBodyText != null) shieldBodyText.text = shieldIntactBody;

                if (shieldBox != null) shieldBox.color = boxNeutralColor;
            }

            if (accentBar != null) accentBar.color = accent;
        }

        // =================================================================
        //  OUTILS
        // =================================================================
        /// <summary>
        /// Secteur de la boussole hexagonale, a partir de la position monde du
        /// Shofar : 0 = est, puis dans le sens trigonometrique tous les 60 degres.
        /// On passe par la position monde et non par les coordonnees cube pour que
        /// le nom colle a ce que le joueur voit a l'ecran.
        /// </summary>
        private int DirectionIndex(Hexagon portal)
        {
            // Le calcul vit dans HexCompass : la carte d'unite nomme les Shofars de
            // la meme facon, et deux copies du meme Atan2 finiraient par diverger.
            return HexCompass.SectorFromWorld(portal.transform.position);
        }

        private void Clear()
        {
            if (_current == null) return;
            _current = null;
            InvalidateCache();
            Hide();
        }

        private void InvalidateCache()
        {
            _lastHp = -1;
            _lastMaxHp = -1;
            _lastKills = -1;
            _lastShield = -1;
            _lastLevel = -1;
            _lastSurge = false;
            _lastTopLevel = -1;
        }

        private void Hide()
        {
            if (panelRoot != null && panelRoot.activeSelf) panelRoot.SetActive(false);
        }
    }
}
