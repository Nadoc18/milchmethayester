using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MNLTHII;
using MNLTHII.Managers;
using MNLTHII.Rules;

/// <summary>
/// Le Yetzer Hara. GDD V3 - Section 2 : "l'IA avance impitoyablement vers ses cibles
/// (la Base ou les allies proches)". Combat strictement deterministe.
/// </summary>
public class EnemyAI : MonoBehaviour
{
    public static EnemyAI Instance { get; private set; }

    [Header("Ciblage du Yetzer Hara (equilibrage)")]
    [Tooltip("Attrait de base de la Base du joueur : c'est elle qui fait gagner l'IA.")]
    public int basePriority = 900;
    [Tooltip("Attrait de base d'un Tank allie rencontre en chemin.")]
    public int tankPriority = 650;
    [Tooltip("Malus par case de distance, applique a toutes les cibles.")]
    public int distanceWeight = 35;
    [Tooltip("Bonus si la cible est deja a portee : attaquer ne coute aucun deplacement.")]
    public int inRangeBonus = 200;
    [Tooltip("Bonus si le coup suffit a detruire la cible ce tour-ci.")]
    public int finishBonus = 250;
    [Tooltip("Attrait d'un batiment du joueur (Bunker, Centre de Commandement) rencontre en chemin.")]
    public int buildingPriority = 430;

    [Tooltip("Attrait des USINES. Au-dessus de la Base : casser le revenu paie plus que frapper un mur.")]
    public int factoryPriority = 980;

    [Tooltip("Attrait des Cristaux. Moins qu'une usine, plus qu'un Bunker.")]
    public int supportPriority = 620;

    [Tooltip("Ce que chaque defenseur a portee retire a l'attrait d'une cible.")]
    public int defenderPenalty = 160;

    [Tooltip("A quelle distance un Tank est considere comme gardant une cible.")]
    public int defenderRadius = 2;
    [Tooltip("Distance au-dela de laquelle un batiment n'interesse plus l'IA.")]
    public int buildingMaxDistance = 3;

    // Tampon reutilise d'un tour a l'autre : FindAll(lambda) allouait une List et
    // une closure a chaque phase ennemie.
    private readonly List<PawnController> _enemyBuffer = new List<PawnController>(24);

    // =====================================================================
    //  RYTHME
    // =====================================================================
    /// <summary>
    /// Memes durees que la phase des Tanks, et c'est voulu : les deux phases
    /// partagent la meme grammaire a l'ecran, elles doivent partager le meme tempo.
    /// Un Yetzer Hara plus rapide donnerait l'impression qu'il triche.
    ///
    /// Le temps de focalisation doit rester SUPERIEUR a CameraDirector.moveDuration,
    /// sinon l'attaque se resout pendant que la camera voyage encore.
    /// </summary>
    [Header("Rythme")]
    public float focusDelay = 0.95f;
    public float aimDelay = 0.35f;
    public float resolveDelay = 1.05f;
    public float moveDelay = 0.85f;
    public float stepDelay = 0.55f;

    private WaitForSeconds _waitFocus, _waitAim, _waitResolve, _waitMove, _waitStep;
    private float _cachedFocus = -1f, _cachedAim = -1f, _cachedResolve = -1f;
    private float _cachedMove = -1f, _cachedStep = -1f;

    private void RefreshPhaseWaits()
    {
        if (!Mathf.Approximately(_cachedFocus, focusDelay))
        { _cachedFocus = focusDelay; _waitFocus = new WaitForSeconds(focusDelay); }

        if (!Mathf.Approximately(_cachedAim, aimDelay))
        { _cachedAim = aimDelay; _waitAim = new WaitForSeconds(aimDelay); }

        if (!Mathf.Approximately(_cachedResolve, resolveDelay))
        { _cachedResolve = resolveDelay; _waitResolve = new WaitForSeconds(resolveDelay); }

        if (!Mathf.Approximately(_cachedMove, moveDelay))
        { _cachedMove = moveDelay; _waitMove = new WaitForSeconds(moveDelay); }

        if (!Mathf.Approximately(_cachedStep, stepDelay))
        { _cachedStep = stepDelay; _waitStep = new WaitForSeconds(stepDelay); }
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(this); return; }
    }

    private void Start()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnEnded += HandleTurnEnded;
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnEnded -= HandleTurnEnded;
    }

    private void HandleTurnEnded(int turnNumber)
    {
        Debug.Log("[EnemyAI] Tour " + turnNumber + " : le Yetzer Hara joue.");
        StartCoroutine(ProcessEnemyUnitsSequential());
    }

    private IEnumerator ProcessEnemyUnitsSequential()
    {
        RefreshPhaseWaits();

        BoardController board = BoardController.instance;
        if (board == null || board.PawnsInBoard == null)
        {
            PassTurnToPlayer();
            yield break;
        }

        _enemyBuffer.Clear();
        List<PawnController> pawns = board.PawnsInBoard;
        for (int i = 0; i < pawns.Count; i++)
        {
            PawnController p = pawns[i];
            if (p != null && p.IsEnemy) _enemyBuffer.Add(p);
        }

        for (int i = 0; i < _enemyBuffer.Count; i++)
        {
            PawnController enemy = _enemyBuffer[i];
            if (enemy == null || enemy.currentHP <= 0) continue;

            if (FXManager.Instance != null) FXManager.Instance.SetUnitFocus(enemy.transform, true);

            // Exactement la meme carte qu'en phase 3, en rouge. Le joueur n'a rien
            // de nouveau a apprendre : il lit sa propre mecanique, retournee.
            UnitActionCard.Focus(enemy, i + 1);
            yield return _waitFocus;

            // Choix de cible pondere : la Base reste l'objectif (c'est elle qui fait
            // gagner le Yetzer Hara), les Tanks rencontres en chemin sont engages.
            PawnController targetPawn;
            Hexagon targetHex;
            SelectEnemyTarget(enemy, out targetPawn, out targetHex);

            if (targetPawn == null && targetHex == null) continue;

            HexCoord targetCoord = (targetPawn != null) ? targetPawn.hexcoord : targetHex.positionInTheBoard;
            int distance = BoardController.GetHexDistance(enemy.hexcoord, targetCoord);

            if (distance <= enemy.attackRange)
            {
                UnitActionCard.Attack(enemy, targetPawn, targetHex);

                Vector3 impact = (targetPawn != null)
                                 ? targetPawn.transform.position
                                 : targetHex.transform.position;
                CameraDirector.FrameAction(enemy.transform.position, impact);

                enemy.attackCoord = targetCoord;
                enemy.ApplySequenceAnimation(PawnController.TypeOfPawnInteractions.attack);

                // Le trait de laser a ete retire : il ne s'affichait pas. La lecture du
                // tir repose maintenant sur ce qui se voit vraiment - la camera qui
                // cadre le duel, l'effet d'impact sur la cible, et le chiffre de degats
                // qui s'envole au-dessus d'elle.

                yield return _waitAim;

                if (targetPawn != null) InteractionRules.ResolveCombat(enemy, targetPawn);
                else InteractionRules.ResolveCombat(enemy, targetHex);

                yield return _waitResolve;
            }
            else
            {
                // Les Centres de Commandement repoussent les ennemis : GetNextStepTowards
                // en tient compte quand isEnemy vaut true.
                UnitActionCard.Move(enemy);

                // Un rang 2 avance de deux cases, un rang 3 de trois. C'est la
                // contrepartie de leur portee ramenee a une case : ils ne pilonnent plus
                // de loin, ils FONCENT. La boucle s'arrete des qu'ils sont au contact.
                int steps = (enemy.moveRange > 0) ? enemy.moveRange : 1;

                for (int s = 0; s < steps; s++)
                {
                    if (BoardController.GetHexDistance(enemy.hexcoord, targetCoord) <= enemy.attackRange) break;

                    HexCoord nextCoord = board.GetNextStepTowards(enemy.hexcoord, targetCoord, true);
                    if (nextCoord == null || nextCoord.CompareHexCoord(enemy.hexcoord)) break;

                    enemy.targetCoord = nextCoord;
                    enemy.ApplySequenceAnimation(PawnController.TypeOfPawnInteractions.target);
                    yield return _waitMove;
                }
            }

            yield return _waitStep;
        }

        if (FXManager.Instance != null) FXManager.Instance.SetUnitFocus(null);
        UnitActionCard.Dismiss();
        PassTurnToPlayer();
    }

    /// <summary>
    /// Choix de cible d'un ennemi. La Base est l'objectif permanent : sans cela, tant
    /// qu'un seul Tank restait en vie l'IA ne progressait jamais vers elle et ne pouvait
    /// pas gagner. Les Tanks croises en chemin restent engages, d'autant plus qu'ils sont
    /// proches ou achevables.
    /// </summary>
    /// <remarks>
    /// Publique et sans effet de bord : ThreatPreview l'appelle pour afficher
    /// l'intention de chaque ennemi. L'apercu et la phase ennemie partagent ainsi
    /// exactement le meme calcul, et l'apercu ne peut pas mentir.
    /// </remarks>
    public void SelectEnemyTarget(PawnController enemy, out PawnController targetPawn, out Hexagon targetHex)
    {
        targetPawn = null;
        targetHex = null;

        BoardController board = BoardController.instance;
        if (board == null || enemy == null || enemy.hexcoord == null) return;

        int bestScore = int.MinValue;
        int damage = MNLTHII.Rules.InteractionRules.GetPawnDamage(enemy);

        // --- La Base : objectif de victoire du Yetzer Hara ---
        List<Hexagon> hexes = board.HexagonsInBoard;
        for (int i = 0; i < hexes.Count; i++)
        {
            Hexagon hex = hexes[i];
            if (hex == null || hex.type != TypeOfHex.Base) continue;

            int distance = BoardController.GetHexDistance(enemy.hexcoord, hex.positionInTheBoard);

            int score = basePriority - distance * distanceWeight;
            if (distance <= enemy.attackRange) score += inRangeBonus;

            if (score > bestScore)
            {
                bestScore = score;
                targetHex = hex;
                targetPawn = null;
            }
        }

        // --- Les Tanks allies rencontres en chemin ---
        List<PawnController> pawns = board.PawnsInBoard;
        for (int i = 0; i < pawns.Count; i++)
        {
            PawnController tank = pawns[i];
            if (tank == null || tank.IsEnemy || tank.hexcoord == null || tank.currentHP <= 0) continue;

            int distance = BoardController.GetHexDistance(enemy.hexcoord, tank.hexcoord);

            int score = tankPriority - distance * distanceWeight;
            if (distance <= enemy.attackRange) score += inRangeBonus;
            if (tank.currentHP <= damage) score += finishBonus;

            if (score > bestScore)
            {
                bestScore = score;
                targetPawn = tank;
                targetHex = board.getHexByCoord(tank.hexcoord);
            }
        }

        // --- LES USINES ET LES CRISTAUX : la vraie cible ---
        //
        // L'Energie ne tombe plus du ciel : elle est produite par les Gaz, poses sur la
        // carte, loin de la Base. Un Yetzer Hara qui l'ignore laisse le joueur se terrer
        // et s'enrichir - c'est exactement ce qui rendait la partie gagnee d'avance.
        //
        // Deux choses pesent dans le choix : ce que la cible RAPPORTE au joueur, et ce
        // qu'elle COUTE a atteindre. Une usine rang 2 sans defense passe avant la Base ;
        // la meme usine derriere trois Tanks ne vaut pas le detour.
        //
        // Pas de limite de distance ici, contrairement aux fortifications : une usine
        // merite qu'on traverse la carte.
        for (int i = 0; i < hexes.Count; i++)
        {
            Hexagon hex = hexes[i];
            if (hex == null || hex.level < 1 || hex.currentHP <= 0) continue;

            bool factory = (hex.type == TypeOfHex.gas);
            bool support = (hex.type == TypeOfHex.crystal);
            if (!factory && !support) continue;

            int distance = BoardController.GetHexDistance(enemy.hexcoord, hex.positionInTheBoard);

            int score = (factory ? factoryPriority : supportPriority) - distance * distanceWeight;

            // Le rang 2 produit le double : il vaut le double.
            if (hex.level >= 2) score += 180;

            if (distance <= enemy.attackRange) score += inRangeBonus;
            if (hex.currentHP <= damage) score += finishBonus;

            // Ce qui la garde la rend moins interessante. C'est ce calcul qui fait
            // choisir a l'ennemi l'usine mal couverte plutot que la mieux defendue.
            score -= CountDefenders(hex) * defenderPenalty;

            if (score > bestScore)
            {
                bestScore = score;
                targetHex = hex;
                targetPawn = null;
            }
        }

        // --- Les batiments du joueur croises de pres ---
        // Sans cela, un mur de Bunkers etait definitif : l'IA le contournait ou
        // s'arretait devant, et la partie se figeait. Un ennemi qui bute sur une
        // fortification s'y attaque, ce qui oblige le joueur a l'entretenir.
        for (int i = 0; i < hexes.Count; i++)
        {
            Hexagon hex = hexes[i];
            if (hex == null || hex.level < 1 || hex.currentHP <= 0) continue;
            if (hex.type != TypeOfHex.hill && hex.type != TypeOfHex.mountain) continue;

            int distance = BoardController.GetHexDistance(enemy.hexcoord, hex.positionInTheBoard);
            if (distance > buildingMaxDistance) continue;

            int score = buildingPriority - distance * distanceWeight;
            if (distance <= enemy.attackRange) score += inRangeBonus;
            if (hex.currentHP <= damage) score += finishBonus;

            if (score > bestScore)
            {
                bestScore = score;
                targetHex = hex;
                targetPawn = null;
            }
        }
    }

    /// <summary>
    /// Combien de Tanks et de Bunkers couvrent cette case.
    ///
    /// C'est la mesure de defense la plus simple qui soit, et elle suffit : elle dit a
    /// l'ennemi quel bien du joueur est le moins bien garde. Un Tank compte pour un,
    /// un Bunker aussi - un Bunker tire plus fort, mais il ne peut pas suivre.
    /// </summary>
    private int CountDefenders(Hexagon hex)
    {
        BoardController board = BoardController.instance;
        if (board == null || hex == null || hex.positionInTheBoard == null) return 0;

        int count = 0;

        List<PawnController> pawns = board.PawnsInBoard;
        if (pawns != null)
        {
            for (int i = 0; i < pawns.Count; i++)
            {
                PawnController tank = pawns[i];
                if (tank == null || tank.IsEnemy || tank.currentHP <= 0 || tank.hexcoord == null) continue;
                if (BoardController.GetHexDistance(hex.positionInTheBoard, tank.hexcoord) <= defenderRadius) count++;
            }
        }

        List<Hexagon> hexes = board.HexagonsInBoard;
        if (hexes != null)
        {
            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon other = hexes[i];
                if (other == null || other.type != TypeOfHex.hill) continue;
                if (other.level < 1 || other.currentHP <= 0) continue;

                int range = MNLTHII.Rules.InteractionRules.BUNKER_RANGE;
                if (BoardController.GetHexDistance(hex.positionInTheBoard, other.positionInTheBoard) <= range) count++;
            }
        }

        return count;
    }

    private void PassTurnToPlayer()
    {
        Debug.Log("[EnemyAI] Fin de la phase ennemie.");
        if (TurnManager.Instance != null) TurnManager.Instance.StartNextTurn();
    }
}
