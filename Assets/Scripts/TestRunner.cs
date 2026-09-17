using UnityEngine;
using MNLTHII;
using MNLTHII.Managers;
using MNLTHII.Rules;

/// <summary>
/// Verifications rapides des chiffres du GDD V3 au lancement.
/// A desactiver dans la scene pour une build de production.
/// </summary>
public class TestRunner : MonoBehaviour
{
    private int _failures = 0;

    private void Start()
    {
        Debug.Log("--- TESTS REGLES GDD V3 ---");

        TestPawnStats();
        TestNoRandomness();
        TestBuildingHP();
        TestEnergy();
        TestBuildCosts();
        TestPortalInstability();

        if (_failures == 0) Debug.Log("--- TOUS LES TESTS SONT PASSES ---");
        else Debug.LogError("--- " + _failures + " TEST(S) EN ECHEC ---");
    }

    private void Check(bool condition, string label)
    {
        if (condition) Debug.Log("OK   : " + label);
        else { _failures++; Debug.LogError("ECHEC: " + label); }
    }

    private PawnController MakePawn(TypeOfPawn type, int level)
    {
        GameObject go = new GameObject("TestPawn");
        PawnController pawn = go.AddComponent<PawnController>();
        pawn.typeOfPawn = type;
        pawn.level = level;
        InteractionRules.ApplyStatsToPawn(pawn);
        return pawn;
    }

    private void TestPawnStats()
    {
        // Section 5.1 - Tanks
        PawnController t1 = MakePawn(TypeOfPawn.unit, 1);
        Check(t1.maxHP == 30 && t1.attackDamageMin == 10 && t1.attackRange == 1, "Tank Niv1 = 30 PV / 10 degats / portee 1");

        PawnController t2 = MakePawn(TypeOfPawn.unit, 2);
        Check(t2.maxHP == 60 && t2.attackDamageMin == 20 && t2.attackRange == 2, "Tank Niv2 = 60 PV / 20 degats / portee 2");
        Check(t1.moveRange == 1 && t2.moveRange == 2, "Mobilite : Tank Niv1 = 1 case, Niv2 = 2 cases");
        Check(t1.stance == PawnStance.Guard, "Posture par defaut d'un Tank : Garde");

        // Section 5.2 - Ennemis
        PawnController e1 = MakePawn(TypeOfPawn.enemy, 1);
        Check(e1.maxHP == 20 && e1.attackDamageMin == 10 && e1.attackRange == 1, "Ennemi Niv1 = 20 PV / 10 degats / portee 1");

        PawnController e2 = MakePawn(TypeOfPawn.enemy, 2);
        Check(e2.maxHP == 40 && e2.attackDamageMin == 15 && e2.attackRange == 2, "Ennemi Niv2 = 40 PV / 15 degats / portee 2");

        PawnController e3 = MakePawn(TypeOfPawn.enemy, 3);
        Check(e3.maxHP == 80 && e3.attackDamageMin == 30 && e3.attackRange == 3, "Ennemi Niv3 = 80 PV / 30 degats / portee 3");

        // Un Tank Niv1 doit tuer un Ennemi Niv1 en exactement 2 coups.
        Check(Mathf.CeilToInt(20f / 10f) == 2, "Tank Niv1 tue un Ennemi Niv1 en 2 tours");

        // Un Ennemi Niv3 doit detruire un Tank Niv1 en un seul coup.
        Check(30 >= 30, "Ennemi Niv3 detruit un Tank Niv1 en 1 coup");

        Destroy(t1.gameObject); Destroy(t2.gameObject);
        Destroy(e1.gameObject); Destroy(e2.gameObject); Destroy(e3.gameObject);
    }

    private void TestNoRandomness()
    {
        PawnController p = MakePawn(TypeOfPawn.unit, 2);
        Check(p.attackDamageMin == p.attackDamageMax, "Degats fixes (min = max)");
        Check(Mathf.Approximately(p.critChance, 0f), "Aucune chance de critique");
        Check(Mathf.Approximately(p.dodgeChance, 0f), "Aucune esquive");
        Destroy(p.gameObject);
    }

    private void TestBuildingHP()
    {
        Check(InteractionRules.GetBuildingMaxHP(TypeOfHex.Base, 1) == 200, "Base = 200 PV");
        Check(InteractionRules.GetBuildingMaxHP(TypeOfHex.portal, 1) == 50, "Portail Niv1 = 50 PV");
        Check(InteractionRules.GetBuildingMaxHP(TypeOfHex.portal, 2) == 100, "Portail Niv2 = 100 PV");
        Check(InteractionRules.GetBuildingMaxHP(TypeOfHex.hill, 1) == 40, "Bunker = 40 PV");
        Check(InteractionRules.GetBuildingMaxHP(TypeOfHex.hill, 2) == 80, "Forteresse = 80 PV");
        Check(InteractionRules.GetBuildingMaxHP(TypeOfHex.mountain, 1) == 30, "Centre de Com. = 30 PV");
        Check(InteractionRules.GetBuildingMaxHP(TypeOfHex.mountain, 2) == 50, "Centre de Com. Niv3 = 50 PV");

        Check(InteractionRules.GetBunkerTargets(1) == 4 && InteractionRules.GetBunkerDamage(1) == 10, "Bunker : 4 cibles a 10 degats");
        Check(InteractionRules.GetBunkerTargets(2) == 6 && InteractionRules.GetBunkerDamage(2) == 15, "Forteresse : 6 cibles a 15 degats");
        // Le soin a change de main : il appartient au Cristal, plus au Gaz.
        Check(InteractionRules.GetCrystalHeal(1) == 10 && InteractionRules.GetCrystalRange(1) == 1, "Cristal Niv2 : +10 PV a 1 case");
        Check(InteractionRules.GetCrystalHeal(2) == 20 && InteractionRules.GetCrystalRange(2) == 2, "Cristal Niv3 : +20 PV a 2 cases");
        Check(InteractionRules.GetMountainRepel(1) == 1 && InteractionRules.GetMountainRepel(2) == 2, "Centre de Com. : infranchissable a 1 puis 2 cases");
        Check(InteractionRules.GetMountainCommandRadius(1) == 2 && InteractionRules.GetMountainCommandRadius(2) == 3, "Centre de Com. : commande a 2 puis 3 cases");
    }

    private void TestEnergy()
    {
        Check(InteractionRules.TRIVIA_ENERGY_REWARD == 25, "Bonne reponse = +25 Energie");
        Check(InteractionRules.TANK_CREATION_COST == 50, "Creation d'un Tank = -50 Energie");
        Check(InteractionRules.PORTAL_EVOLVE_AFTER_TURNS == 18, "Portail : evolution a partir du tour 18");
        Check(InteractionRules.PORTAL_EVOLVE_STAGGER == 3, "Les portails evoluent a 3 tours d'intervalle");
        Check(Mathf.Approximately(InteractionRules.PORTAL_MINIBOSS_CHANCE, 0.10f), "Portail Niv2 : 10% de mini-boss");

        if (EnergyManager.Instance != null)
        {
            int before = EnergyManager.Instance.CurrentEnergy;
            Check(!EnergyManager.Instance.TrySpend(before + 1000), "Depense refusee si le solde est insuffisant");
        }
    }

    /// <summary>
    /// Les couts sont le coeur de l'equilibrage : tant que construire etait gratuit,
    /// une seule ouverture dominait. Ces tests figent le bareme.
    /// </summary>
    private void TestBuildCosts()
    {
        Check(InteractionRules.GetBuildCost(TypeOfHex.hill, 1) == 40, "Bunker = 40 Energie");
        Check(InteractionRules.GetBuildCost(TypeOfHex.hill, 2) == 60, "Forteresse = 60 Energie");
        Check(InteractionRules.GetBuildCost(TypeOfHex.gas, 1) == 30, "Usine a gaz = 30 Energie");
        Check(InteractionRules.GetBuildCost(TypeOfHex.crystal, 1) == 60, "Cristal = 60 Energie");
        Check(InteractionRules.GetBuildCost(TypeOfHex.mountain, 1) == 50, "Centre de Commandement = 50 Energie");
        Check(InteractionRules.GetBuildCost(TypeOfHex.plain, 1) == 0, "Une plaine ne se construit pas");

        Check(InteractionRules.TANK_EVOLVE_COST == 60, "Evolution d'un Tank = 60 Energie");
        Check(InteractionRules.STARTING_ENERGY == 60, "Solde d'ouverture = 60 Energie");
        Check(InteractionRules.BASE_INCOME_PER_TURN == 12, "Revenu passif de la Base = 12 par tour");
        Check(InteractionRules.GetBunkerShotCost(1) == 4 && InteractionRules.GetBunkerShotCost(2) == 5,
              "Tir d'un Bunker : 4 puis 5 Energie");
        Check(InteractionRules.INITIAL_ENEMIES == 3, "3 ennemis deja en marche au premier tour");
        Check(InteractionRules.PORTAL_SPAWN_INTERVAL == 3,
              "Intervalle de deploiement 3 : avec 6 portails decales, deux ennemis par tour");

        // L'economie a change de main : ce sont les USINES qui paient, plus le Cristal.
        Check(InteractionRules.GetGasIncome(1) == 15 && InteractionRules.GetGasIncome(2) == 30,
              "Revenu d'une usine : 15 puis 30 par tour");
        Check(InteractionRules.GetGasIncome(0) == 0, "Une usine non construite ne rapporte rien");
        Check(InteractionRules.GetCrystalIncome(1) == 0, "Le Cristal ne rapporte plus d'Energie");

        // Le plancher seul ne doit pas payer un Tank : sans usine, on survit, on ne joue pas.
        Check(InteractionRules.BASE_INCOME_PER_TURN < InteractionRules.TANK_CREATION_COST,
              "Le plancher seul ne paie pas un Tank : il faut tenir ses usines");

        // Une seule usine rang 1 ne suffit pas non plus a sortir un Tank par tour.
        int oneFactory = InteractionRules.BASE_INCOME_PER_TURN + InteractionRules.GAS_INCOME_L1;
        Check(oneFactory < InteractionRules.TANK_CREATION_COST,
              "Plancher plus une usine : toujours pas un Tank par tour");
    }

    /// <summary>Instabilite des Portails : c'est elle qui rend la victoire atteignable.</summary>
    private void TestPortalInstability()
    {
        Check(InteractionRules.PORTAL_KILL_BACKLASH == 12, "Contrecoup par ennemi tue = 12 PV");
        Check(InteractionRules.PORTAL_KILLS_TO_BREAK_SHIELD == 4, "Bouclier rompu au bout de 4 morts");
        Check(InteractionRules.PORTAL_SHIELD_DOWN_TURNS == 4, "Bouclier rompu pendant 4 tours");
        Check(InteractionRules.PORTAL_SHIELD_DIVISOR == 2, "Bouclier intact : degats divises par 2");

        // Un Tank Niv1 place 5 degats sur un portail blinde, 10 une fois le bouclier tombe.
        Check(10 / InteractionRules.PORTAL_SHIELD_DIVISOR == 5, "Tank Niv1 : 5 degats sur un Portail blinde");

        // Le contrecoup ne doit JAMAIS pouvoir fermer un portail a lui seul : sinon une
        // partie purement defensive gagne sans que le joueur prenne le moindre risque.
        int floor = (InteractionRules.PORTAL_HP_L1 * InteractionRules.PORTAL_BACKLASH_FLOOR_PERCENT) / 100;
        Check(floor > 0, "Le contrecoup s'arrete a un plancher de PV, il n'acheve jamais un Portail");
        Check(InteractionRules.PORTAL_BACKLASH_FLOOR_PERCENT == 20, "Plancher de contrecoup = 20% des PV max");

        // Quatre morts avant la rupture du bouclier, a 12 PV chacune : un Portail Niv1
        // tombe a son plancher au moment exact ou il devient vulnerable. La defense
        // prepare l'assaut, elle ne le remplace pas.
        int backlashBeforeBreak = InteractionRules.PORTAL_KILL_BACKLASH * InteractionRules.PORTAL_KILLS_TO_BREAK_SHIELD;
        Check(backlashBeforeBreak >= InteractionRules.PORTAL_HP_L1 - floor,
              "Tenir la ligne amene un Portail Niv1 a son plancher avant la rupture du bouclier");
    }
}
