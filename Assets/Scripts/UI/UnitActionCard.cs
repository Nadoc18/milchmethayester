using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MNLTHII;
using MNLTHII.Rules;

namespace MNLTHII.Managers
{
    /// <summary>
    /// La carte d'unite - phases 3 (Tanks) et 4 (Yetzer Hara).
    ///
    /// UNE SEULE carte pour les deux phases, et c'est delibere au sens fort : la
    /// maquette impose exactement la meme grammaire des deux cotes, seule la couleur
    /// change. Le joueur apprend la lecture une fois, en phase 3, et la phase 4 ne
    /// lui demande plus aucun effort - il voit tout de suite que c'est la meme
    /// mecanique retournee contre lui. Deux panneaux differents auraient detruit ca.
    ///
    /// Ce que la carte doit dire, et pourquoi :
    ///
    ///   QUI agit      - le projecteur suit deja l'unite, la carte la nomme.
    ///   SA POSTURE    - c'est la seule decision que le joueur ait prise sur ce tank.
    ///                   La voir ici, au moment ou elle produit son effet, est ce qui
    ///                   la justifie : sinon la phase automatique parait arbitraire.
    ///   SUR QUOI      - nomme, jamais en coordonnees.
    ///   COMBIEN       - le chiffre exact au moment de l'impact, bouclier compris.
    ///   SON SHOFAR    - cote ennemi seulement, et c'est la ligne qui rend la boucle
    ///                   d'ebranlement lisible : tuer CET ennemi frappera CE Shofar.
    ///
    /// Ce composant ne cree rien et ne decide rien. TurnManager et EnemyAI lui
    /// disent ce qui se passe ; il l'affiche. Tous les libelles hebreux viennent de
    /// l'inspecteur, donc de hud_labels.json, et ce .cs reste en pur ASCII.
    ///
    /// Note d'optimisation : aucun Update. La carte ne travaille que sur appel, une
    /// poignee de fois par tour. Les nombres passent par SetText(format, valeur),
    /// qui ecrit dans le tampon de char de TextMeshPro sans allouer de string ;
    /// string.Format ou la concatenation en auraient alloue une par unite et par
    /// tour, soit une quinzaine de kilo-octets de dechets sur une partie.
    /// </summary>
    public class UnitActionCard : MonoBehaviour
    {
        /// <summary>
        /// Instance unique : TurnManager et EnemyAI appellent la carte depuis leurs
        /// boucles, et aucun des deux ne doit dependre d'une reference d'inspecteur
        /// qui pourrait etre vide.
        /// </summary>
        public static UnitActionCard Instance;

        // =================================================================
        //  REFERENCES
        // =================================================================
        [Header("Racine")]
        public GameObject panelRoot;
        public Image accentBar;

        [Header("Entete")]
        public TMPro.TextMeshProUGUI titleText;
        public TMPro.TextMeshProUGUI subtitleText;
        public TMPro.TextMeshProUGUI hpText;
        public Image hpFill;

        [Header("Lignes de detail")]
        public TMPro.TextMeshProUGUI[] rowLabels = new TMPro.TextMeshProUGUI[4];
        public TMPro.TextMeshProUGUI[] rowValues = new TMPro.TextMeshProUGUI[4];
        public TMPro.TextMeshProUGUI[] rowUnits = new TMPro.TextMeshProUGUI[4];

        [Header("Chiffre flottant")]
        public GameObject flashRoot;
        public TMPro.TextMeshProUGUI flashValueText;
        public TMPro.TextMeshProUGUI flashCaptionText;

        // =================================================================
        //  LIBELLES - alimentes par hud_labels.json
        // =================================================================
        [Header("Libelles")]
        public string tankNameFormat = "{0}";
        public string enemyName = "";
        public string levelFormat = "{0}";
        public string hpFormat = "{0}/{1}";

        // Guard, Assault, Hunt - dans l'ordre de l'enum PawnStance.
        public string[] stanceNames = new string[3];

        // Est, nord-est, nord-ouest, ouest, sud-ouest, sud-est.
        public string[] directionNames = new string[6];

        public string rowTargetLabel = "";
        public string rowDamageLabel = "";
        public string rowRangeLabel = "";
        public string rowMobilityLabel = "";
        public string rowOriginLabel = "";

        public string unitHex = "";
        public string unitHexPerTurn = "";

        public string targetBase = "";
        public string targetEnemy = "";
        public string targetTank = "";

        public string captionDamage = "";
        public string captionShielded = "";
        public string captionBaseHp = "";
        public string captionMove = "";

        // Nombre de degats reduit par le bouclier : "10 -> 5". La fleche est un
        // caractere ASCII ici ; le JSON peut lui substituer une vraie fleche.
        public string damageReducedFormat = "{0} / {1}";

        // =================================================================
        //  COULEURS
        // =================================================================
        [Header("Couleurs")]
        public Color allyColor = new Color(0.24f, 0.88f, 0.82f);
        public Color enemyColor = new Color(1f, 0.30f, 0.37f);
        public Color textColor = new Color(0.90f, 0.93f, 1f);

        // =================================================================
        //  CYCLE DE VIE
        // =================================================================
        private void Awake()
        {
            if (Instance == null) Instance = this;

            // Pas de repli sur gameObject : ce composant vit sur le Canvas, et un
            // SetActive(false) dessus eteindrait toute l'interface.
            if (panelRoot == null)
                Debug.LogWarning("[UnitCard] panelRoot n'est pas renseigne : la carte restera muette.");

            Close();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // =================================================================
        //  FACADE STATIQUE - ce que les boucles de tour appellent
        // =================================================================
        public static void Focus(PawnController actor, int ordinal)
        {
            if (Instance != null) Instance.Begin(actor, ordinal);
        }

        public static void Attack(PawnController actor, PawnController targetPawn, Hexagon targetHex)
        {
            if (Instance != null) Instance.SetAttack(actor, targetPawn, targetHex);
        }

        public static void Move(PawnController actor)
        {
            if (Instance != null) Instance.SetMove(actor);
        }

        public static void Dismiss()
        {
            if (Instance != null) Instance.Close();
        }

        // =================================================================
        //  AFFICHAGE
        // =================================================================
        /// <summary>
        /// Ouvre la carte sur l'unite qui prend la main. ordinal est son rang dans
        /// la phase, a partir de 1 : c'est ce qui donne "Tank 03". Numeroter par
        /// l'identifiant d'instance aurait donne un nombre different a chaque
        /// partie, et le joueur n'aurait pas pu suivre une unite d'un tour a l'autre.
        /// </summary>
        public void Begin(PawnController actor, int ordinal)
        {
            if (actor == null) { Close(); return; }

            bool enemy = actor.IsEnemy;
            Color accent = enemy ? enemyColor : allyColor;

            if (accentBar != null) accentBar.color = accent;

            // --- nom ---
            if (titleText != null)
            {
                if (enemy) titleText.text = enemyName;
                else titleText.SetText(tankNameFormat, ordinal);
            }

            // --- sous-titre : la posture cote joueur, le niveau cote ennemi ---
            if (subtitleText != null)
            {
                if (enemy) subtitleText.SetText(levelFormat, actor.level);
                else subtitleText.text = StanceName(actor.stance);

                subtitleText.color = accent;
            }

            // --- points de vie ---
            int hp = actor.currentHP;
            int maxHp = actor.maxHP > 0 ? actor.maxHP : hp;

            if (hpText != null) hpText.SetText(hpFormat, hp, maxHp);
            if (hpFill != null)
            {
                hpFill.color = accent;
                hpFill.fillAmount = (maxHp > 0) ? Mathf.Clamp01((float)hp / maxHp) : 0f;
            }

            HideFlash();
            SetRowsEmpty();

            if (panelRoot != null) panelRoot.SetActive(true);
        }

        /// <summary>
        /// L'unite frappe. On affiche la cible nommee, les degats reellement
        /// appliques, la portee, et - cote ennemi - le Shofar dont il est sorti.
        /// </summary>
        public void SetAttack(PawnController actor, PawnController targetPawn, Hexagon targetHex)
        {
            if (actor == null) return;

            bool enemy = actor.IsEnemy;
            Color accent = enemy ? enemyColor : allyColor;

            int raw = actor.attackDamageMin;

            // Le bouclier d'un Shofar intact divise les degats. C'est LA regle que le
            // joueur doit voir a l'instant ou elle s'applique, sinon il croit que son
            // tank tape moins fort sans raison.
            bool shielded = false;
            int applied = raw;

            if (targetHex != null && targetHex.type == TypeOfHex.portal)
            {
                PortalManager portals = PortalManager.Instance;
                shielded = (portals == null) || !portals.IsShieldDown(targetHex);

                if (shielded && InteractionRules.PORTAL_SHIELD_DIVISOR > 0)
                    applied = raw / InteractionRules.PORTAL_SHIELD_DIVISOR;
            }

            // --- ligne 0 : la cible, nommee ---
            SetRow(0, rowTargetLabel, TargetName(targetPawn, targetHex), "", accent);

            // --- ligne 1 : les degats ---
            if (shielded && applied != raw)
            {
                SetRowFormatted(1, rowDamageLabel, damageReducedFormat, raw, applied, "", accent);
            }
            else
            {
                SetRowNumber(1, rowDamageLabel, applied, "", accent);
            }

            // --- ligne 2 : la portee ---
            SetRowNumber(2, rowRangeLabel, actor.attackRange, unitHex, textColor);

            // --- ligne 3 : mobilite cote joueur, Shofar d'origine cote ennemi ---
            if (enemy)
                SetRow(3, rowOriginLabel, OriginPortalName(actor), "", accent);
            else
                SetRowNumber(3, rowMobilityLabel, actor.moveRange, unitHexPerTurn, textColor);

            // --- le chiffre flottant ---
            string caption;
            if (targetHex != null && targetHex.type == TypeOfHex.Base) caption = captionBaseHp;
            else if (shielded && applied != raw) caption = captionShielded;
            else caption = captionDamage;

            ShowFlash(enemy ? -applied : applied, caption, accent);
        }

        /// <summary>L'unite ne peut pas frapper : elle avance. Pas de chiffre flottant.</summary>
        public void SetMove(PawnController actor)
        {
            if (actor == null) return;

            bool enemy = actor.IsEnemy;
            Color accent = enemy ? enemyColor : allyColor;

            SetRow(0, rowTargetLabel, "", "", accent);
            SetRowNumber(1, rowMobilityLabel, actor.moveRange, unitHexPerTurn, textColor);
            SetRowNumber(2, rowRangeLabel, actor.attackRange, unitHex, textColor);

            if (enemy) SetRow(3, rowOriginLabel, OriginPortalName(actor), "", accent);
            else SetRow(3, "", "", "", textColor);

            HideFlash();

            if (flashCaptionText != null) flashCaptionText.text = captionMove;
        }

        public void Close()
        {
            HideFlash();
            if (panelRoot != null && panelRoot.activeSelf) panelRoot.SetActive(false);
        }

        // =================================================================
        //  LIGNES
        // =================================================================
        private void SetRowsEmpty()
        {
            for (int i = 0; i < 4; i++) SetRow(i, "", "", "", textColor);
        }

        private void SetRow(int index, string label, string value, string unit, Color valueColor)
        {
            if (rowLabels != null && index < rowLabels.Length && rowLabels[index] != null)
                rowLabels[index].text = label;

            if (rowValues != null && index < rowValues.Length && rowValues[index] != null)
            {
                rowValues[index].text = value;
                rowValues[index].color = valueColor;
            }

            if (rowUnits != null && index < rowUnits.Length && rowUnits[index] != null)
                rowUnits[index].text = unit;
        }

        private void SetRowNumber(int index, string label, int value, string unit, Color valueColor)
        {
            if (rowLabels != null && index < rowLabels.Length && rowLabels[index] != null)
                rowLabels[index].text = label;

            if (rowValues != null && index < rowValues.Length && rowValues[index] != null)
            {
                rowValues[index].SetText("{0}", value);
                rowValues[index].color = valueColor;
            }

            if (rowUnits != null && index < rowUnits.Length && rowUnits[index] != null)
                rowUnits[index].text = unit;
        }

        private void SetRowFormatted(int index, string label, string format, int a, int b,
                                     string unit, Color valueColor)
        {
            if (rowLabels != null && index < rowLabels.Length && rowLabels[index] != null)
                rowLabels[index].text = label;

            if (rowValues != null && index < rowValues.Length && rowValues[index] != null)
            {
                rowValues[index].SetText(format, a, b);
                rowValues[index].color = valueColor;
            }

            if (rowUnits != null && index < rowUnits.Length && rowUnits[index] != null)
                rowUnits[index].text = unit;
        }

        // =================================================================
        //  CHIFFRE FLOTTANT
        // =================================================================
        private void ShowFlash(int value, string caption, Color color)
        {
            if (flashValueText != null)
            {
                flashValueText.SetText("{0}", value);
                flashValueText.color = color;
            }

            if (flashCaptionText != null) flashCaptionText.text = caption;
            if (flashRoot != null) flashRoot.SetActive(true);
        }

        private void HideFlash()
        {
            if (flashRoot != null && flashRoot.activeSelf) flashRoot.SetActive(false);
        }

        // =================================================================
        //  NOMS
        // =================================================================
        private string StanceName(PawnStance stance)
        {
            int i = (int)stance;
            if (stanceNames == null || i < 0 || i >= stanceNames.Length) return "";
            return stanceNames[i];
        }

        /// <summary>
        /// La cible, nommee. Jamais de coordonnees : "(4,-2)" ne dit rien au joueur,
        /// "le Shofar du nord-est" lui dit tout.
        /// </summary>
        private string TargetName(PawnController targetPawn, Hexagon targetHex)
        {
            if (targetPawn != null) return targetPawn.IsEnemy ? targetEnemy : targetTank;
            if (targetHex == null) return "";

            if (targetHex.type == TypeOfHex.Base) return targetBase;

            if (targetHex.type == TypeOfHex.portal)
                return HexCompass.NameFromWorld(targetHex.transform.position, directionNames);

            return "";
        }

        /// <summary>
        /// Le Shofar dont cet ennemi est sorti. C'est la ligne qui rend l'ebranlement
        /// lisible : elle dit au joueur ou ira le contrecoup s'il abat cette unite.
        /// </summary>
        private string OriginPortalName(PawnController enemy)
        {
            if (enemy == null || !enemy.HasOriginPortal) return "";

            BoardController board = BoardController.instance;
            if (board == null || board.HexagonsInBoard == null) return "";

            List<Hexagon> hexes = board.HexagonsInBoard;
            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex == null || hex.positionInTheBoard == null) continue;

                if (hex.positionInTheBoard.q == enemy.originPortalQ
                    && hex.positionInTheBoard.r == enemy.originPortalR)
                    return HexCompass.NameFromWorld(hex.transform.position, directionNames);
            }

            return "";
        }
    }
}
