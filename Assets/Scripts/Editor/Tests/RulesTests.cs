#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;
using MNLTHII;
using MNLTHII.Managers;
using MNLTHII.Rules;

namespace MNLTHII.Tests
{
    /// <summary>
    /// LES REGLES, FIGEES PAR DES TESTS. Chaque chiffre du GDD qui compte pour
    /// l'equilibrage est ici ; si quelqu'un le change sans le vouloir, le Test Runner
    /// le dit avant le joueur. Window > General > Test Runner > EditMode > Run All.
    /// </summary>
    public class RulesTests
    {
        private DifficultyLevel _savedDifficulty;

        [SetUp]
        public void SetUp()
        {
            _savedDifficulty = GameDifficulty.Current;
        }

        [TearDown]
        public void TearDown()
        {
            // La difficulte choisie par le joueur est dans PlayerPrefs : on la lui rend.
            GameDifficulty.Current = _savedDifficulty;
        }

        private static PawnController MakePawn(TypeOfPawn type, int level)
        {
            GameObject go = new GameObject("TestPawn");
            PawnController pawn = go.AddComponent<PawnController>();
            pawn.typeOfPawn = type;
            pawn.level = level;
            InteractionRules.ApplyStatsToPawn(pawn);
            return pawn;
        }

        private static void Kill(PawnController p)
        {
            if (p != null) Object.DestroyImmediate(p.gameObject);
        }

        // =================================================================
        //  PIONS
        // =================================================================
        [Test]
        public void Tank_Rang1_Et_Rang2()
        {
            PawnController t1 = MakePawn(TypeOfPawn.unit, 1);
            PawnController t2 = MakePawn(TypeOfPawn.unit, 2);

            Assert.AreEqual(30, t1.maxHP, "Tank rang 1 : PV");
            Assert.AreEqual(10, t1.attackDamageMin, "Tank rang 1 : degats");
            Assert.AreEqual(1, t1.attackRange, "Tank rang 1 : portee");
            Assert.AreEqual(1, t1.moveRange, "Tank rang 1 : mobilite");
            Assert.AreEqual(PawnStance.Guard, t1.stance, "posture par defaut : Garde");

            Assert.AreEqual(60, t2.maxHP, "Tank rang 2 : PV");
            Assert.AreEqual(20, t2.attackDamageMin, "Tank rang 2 : degats");
            Assert.AreEqual(2, t2.attackRange, "Tank rang 2 : portee");
            Assert.AreEqual(2, t2.moveRange, "Tank rang 2 : mobilite");

            Assert.AreEqual(t1.maxHP, t1.currentHP, "un pion neuf est a pleine vie");

            Kill(t1); Kill(t2);
        }

        [Test]
        public void Ennemis_A_Chaque_Difficulte()
        {
            foreach (DifficultyLevel d in new[] { DifficultyLevel.Easy, DifficultyLevel.Medium, DifficultyLevel.Hard })
            {
                GameDifficulty.Current = d;
                int hpPct = GameDifficulty.EnemyHpPercent;
                int dmgPct = GameDifficulty.EnemyDamagePercent;

                PawnController e1 = MakePawn(TypeOfPawn.enemy, 1);
                PawnController e2 = MakePawn(TypeOfPawn.enemy, 2);
                PawnController e3 = MakePawn(TypeOfPawn.enemy, 3);

                Assert.AreEqual(GameDifficulty.Scale(20, hpPct), e1.maxHP, d + " : ennemi rang 1 PV");
                Assert.AreEqual(GameDifficulty.Scale(10, dmgPct), e1.attackDamageMin, d + " : ennemi rang 1 degats");
                Assert.AreEqual(GameDifficulty.Scale(40, hpPct), e2.maxHP, d + " : ennemi rang 2 PV");
                Assert.AreEqual(GameDifficulty.Scale(15, dmgPct), e2.attackDamageMin, d + " : ennemi rang 2 degats");
                Assert.AreEqual(GameDifficulty.Scale(80, hpPct), e3.maxHP, d + " : ennemi rang 3 PV");
                Assert.AreEqual(GameDifficulty.Scale(30, dmgPct), e3.attackDamageMin, d + " : ennemi rang 3 degats");

                Assert.AreEqual(1, e1.attackRange, d + " : les ennemis frappent a 1 case");
                Assert.AreEqual(1, e2.attackRange, d + " : les ennemis frappent a 1 case");
                Assert.AreEqual(1, e3.attackRange, d + " : les ennemis frappent a 1 case");
                Assert.AreEqual(1, e1.moveRange, d + " : rang 1 avance de 1");
                Assert.AreEqual(2, e2.moveRange, d + " : rang 2 avance de 2");
                Assert.AreEqual(3, e3.moveRange, d + " : rang 3 avance de 3");

                Assert.IsTrue(e1.maxHP > 0 && e1.attackDamageMin > 0, d + " : l'echelle ne descend jamais a zero");

                // Un Tank rang 1 tue un ennemi rang 1 en deux coups au plus.
                Assert.LessOrEqual(Mathf.CeilToInt((float)e1.maxHP / 10f), 2, d + " : Tank rang 1 tue un ennemi rang 1 en 2 coups");

                Kill(e1); Kill(e2); Kill(e3);
            }
        }

        [Test]
        public void Aucun_Hasard_Dans_Le_Combat()
        {
            PawnController p = MakePawn(TypeOfPawn.unit, 2);
            Assert.AreEqual(p.attackDamageMin, p.attackDamageMax, "degats fixes");
            Assert.AreEqual(0f, p.critChance, 0.0001f, "pas de critique");
            Assert.AreEqual(0f, p.dodgeChance, 0.0001f, "pas d'esquive");
            Assert.AreEqual(p.attackDamageMin, InteractionRules.GetPawnDamage(p), "GetPawnDamage = degats du pion");
            Assert.AreEqual(0, InteractionRules.GetPawnDamage(null), "GetPawnDamage(null) = 0");
            Kill(p);
        }

        [Test]
        public void Difficulte_Echelle()
        {
            Assert.AreEqual(100, GameDifficulty.Scale(100, 100));
            Assert.AreEqual(70, GameDifficulty.Scale(100, 70));
            Assert.GreaterOrEqual(GameDifficulty.Scale(1, 70), 1, "l'echelle ne fait jamais tomber une valeur a 0");

            GameDifficulty.Current = DifficultyLevel.Easy;
            Assert.AreEqual(1, GameDifficulty.Pick(1, 2, 3));
            GameDifficulty.Current = DifficultyLevel.Medium;
            Assert.AreEqual(2, GameDifficulty.Pick(1, 2, 3));
            GameDifficulty.Current = DifficultyLevel.Hard;
            Assert.AreEqual(3, GameDifficulty.Pick(1, 2, 3));
        }

        // =================================================================
        //  BATIMENTS
        // =================================================================
        [Test]
        public void PV_Des_Batiments()
        {
            Assert.AreEqual(200, InteractionRules.GetBuildingMaxHP(TypeOfHex.Base, 1), "Base");
            Assert.AreEqual(InteractionRules.PORTAL_HP_L1, InteractionRules.GetBuildingMaxHP(TypeOfHex.portal, 1), "Shofar rang 1");
            Assert.AreEqual(100, InteractionRules.GetBuildingMaxHP(TypeOfHex.portal, 2), "Shofar rang 2");
            Assert.AreEqual(40, InteractionRules.GetBuildingMaxHP(TypeOfHex.hill, 1), "Bunker");
            Assert.AreEqual(80, InteractionRules.GetBuildingMaxHP(TypeOfHex.hill, 2), "Forteresse");
            Assert.AreEqual(45, InteractionRules.GetBuildingMaxHP(TypeOfHex.mountain, 1), "Centre rang 1");
            Assert.AreEqual(75, InteractionRules.GetBuildingMaxHP(TypeOfHex.mountain, 2), "Centre rang 2");
        }

        [Test]
        public void Couts_De_Construction()
        {
            Assert.AreEqual(75, InteractionRules.GetBuildCost(TypeOfHex.hill, 1), "Bunker");
            Assert.AreEqual(95, InteractionRules.GetBuildCost(TypeOfHex.hill, 2), "Forteresse");
            Assert.AreEqual(30, InteractionRules.GetBuildCost(TypeOfHex.gas, 1), "Usine");
            Assert.AreEqual(60, InteractionRules.GetBuildCost(TypeOfHex.crystal, 1), "Cristal");
            Assert.AreEqual(50, InteractionRules.GetBuildCost(TypeOfHex.mountain, 1), "Centre");
            Assert.AreEqual(0, InteractionRules.GetBuildCost(TypeOfHex.plain, 1), "une plaine ne se construit pas");
            Assert.AreEqual(0, InteractionRules.GetBuildCost(TypeOfHex.desert, 1), "un desert ne se construit pas");
            Assert.AreEqual(40, InteractionRules.TANK_CREATION_COST, "Tank");
            Assert.AreEqual(60, InteractionRules.TANK_EVOLVE_COST, "evolution");

            Assert.Less(InteractionRules.TANK_CREATION_COST, InteractionRules.GetBuildCost(TypeOfHex.hill, 1),
                        "sortir (Tank) coute moins cher que se terrer (Bunker)");
        }

        [Test]
        public void Economie()
        {
            foreach (DifficultyLevel d in new[] { DifficultyLevel.Easy, DifficultyLevel.Medium, DifficultyLevel.Hard })
            {
                GameDifficulty.Current = d;
                Assert.GreaterOrEqual(InteractionRules.STARTING_ENERGY,
                                      InteractionRules.TANK_CREATION_COST + InteractionRules.GetBuildCost(TypeOfHex.gas, 1),
                                      d + " : le solde d'ouverture paie une usine et un Tank");
                Assert.Greater(InteractionRules.BASE_INCOME_PER_TURN, 0, d + " : la Base rapporte");
                Assert.Less(InteractionRules.BASE_INCOME_PER_TURN, InteractionRules.TANK_CREATION_COST,
                            d + " : le plancher seul ne paie pas un Tank par tour");
                Assert.GreaterOrEqual(InteractionRules.PORTAL_EVOLVE_AFTER_TURNS, 10, d + " : aucun Shofar rang 2 avant le tour 10");
                Assert.GreaterOrEqual(InteractionRules.PORTAL_SPAWN_INTERVAL, InteractionRules.PORTAL_SPAWN_INTERVAL_FLOOR,
                                      d + " : le deploiement ne descend pas sous le plancher");
                Assert.Greater(InteractionRules.MAX_SPAWNS_PER_TURN, 0, d + " : une vague deploie au moins un ennemi");
                Assert.Greater(InteractionRules.MAX_ACTIVE_ENEMIES, InteractionRules.MAX_SPAWNS_PER_TURN, d + " : le plafond d'ennemis depasse une vague");
            }

            Assert.AreEqual(15, InteractionRules.GetGasIncome(1), "usine rang 1");
            Assert.AreEqual(30, InteractionRules.GetGasIncome(2), "usine rang 2");
            Assert.AreEqual(0, InteractionRules.GetGasIncome(0), "gaz brut : rien");
            Assert.AreEqual(0, InteractionRules.GetCrystalIncome(1), "le Cristal ne rapporte pas d'Energie");
            Assert.AreEqual(10, InteractionRules.GetCrystalHeal(1));
            Assert.AreEqual(20, InteractionRules.GetCrystalHeal(2));
            Assert.AreEqual(1, InteractionRules.GetCrystalRange(1));
            Assert.AreEqual(2, InteractionRules.GetCrystalRange(2));
            Assert.AreEqual(0, InteractionRules.GetBunkerShotCost(1), "un tir de Bunker est gratuit");
            Assert.AreEqual(0, InteractionRules.GetBunkerDecay(1), "un Bunker au calme ne s'use pas");
            Assert.Greater(InteractionRules.BUNKER_WEAR_PER_SHOT, 0, "mais chaque tir l'use");
            Assert.AreEqual(4, InteractionRules.GetBunkerTargets(1));
            Assert.AreEqual(10, InteractionRules.GetBunkerDamage(1));
            Assert.AreEqual(6, InteractionRules.GetBunkerTargets(2));
            Assert.AreEqual(15, InteractionRules.GetBunkerDamage(2));
            Assert.AreEqual(25, InteractionRules.TRIVIA_ENERGY_REWARD);
            Assert.AreEqual(0, InteractionRules.INITIAL_ENEMIES, "plateau vide au depart");
            Assert.AreEqual(0, InteractionRules.INITIAL_PLAYER_TANKS, "plateau vide au depart");
        }

        [Test]
        public void Centre_De_Commandement()
        {
            Assert.AreEqual(1, InteractionRules.GetMountainRepel(1), "infranchissable a 1 case");
            Assert.AreEqual(InteractionRules.GetMountainRepel(1), InteractionRules.GetMountainRepel(2), "le rang 2 ne repousse pas plus loin");
            Assert.AreEqual(2, InteractionRules.GetMountainCommandRadius(1));
            Assert.AreEqual(3, InteractionRules.GetMountainCommandRadius(2));
            Assert.AreEqual(0, InteractionRules.MOUNTAIN_DECAY_PER_TURN, "un Centre ne fond pas");
            Assert.Greater(InteractionRules.GetMountainShield(1), 0, "rang 1 : une bulle");
            Assert.GreaterOrEqual(InteractionRules.GetMountainShield(2), InteractionRules.GetMountainShield(1), "rang 2 : bulle au moins aussi grosse");
            Assert.AreEqual(0, InteractionRules.GetMountainShield(0), "montagne nue : pas de bulle");
        }

        [Test]
        public void Bulle_Du_Centre_Absorbe_Puis_Cede()
        {
            GameObject go = new GameObject("TestHex");
            Hexagon hex = go.AddComponent<Hexagon>();
            hex.type = TypeOfHex.mountain;
            hex.level = 1;
            hex.positionInTheBoard = new HexCoord(0, 0, 0);
            hex.shieldMax = 30;
            hex.shieldHP = 30;

            Assert.AreEqual(0, InteractionRules.FilterMountainDamage(hex, 10), "la bulle encaisse tout");
            Assert.AreEqual(20, hex.shieldHP);
            Assert.AreEqual(0, InteractionRules.FilterMountainDamage(hex, 25), "le coup qui perce ne deborde pas sur le batiment");
            Assert.AreEqual(0, hex.shieldHP, "la bulle est a zero, jamais negative");
            Assert.AreEqual(7, InteractionRules.FilterMountainDamage(hex, 7), "bulle percee : tout passe");

            hex.type = TypeOfHex.hill;
            hex.shieldHP = 30;
            Assert.AreEqual(9, InteractionRules.FilterMountainDamage(hex, 9), "une colline n'a pas de bulle");

            Object.DestroyImmediate(go);
        }

        // =================================================================
        //  REPARATION DE LA BASE : le puits de depense
        // =================================================================
        [Test]
        public void Reparer_La_Base_Coute_Plus_Cher_Qu_Un_Tank()
        {
            Assert.AreEqual(50, InteractionRules.BASE_REPAIR_COST, "reparer coute 50");
            Assert.AreEqual(25, InteractionRules.BASE_REPAIR_AMOUNT, "reparer rend 25 PV");

            // Le prix est mauvais expres : reparer ne doit jamais etre la premiere
            // depense, seulement celle de l'Energie qui dort.
            Assert.Greater(InteractionRules.BASE_REPAIR_COST, InteractionRules.TANK_CREATION_COST,
                           "reparer coute plus cher que poser un Tank");

            // Et il faut plus d'un tour de revenu : on ne repare pas en continu.
            foreach (DifficultyLevel d in new[] { DifficultyLevel.Easy, DifficultyLevel.Medium, DifficultyLevel.Hard })
            {
                GameDifficulty.Current = d;
                Assert.Greater(InteractionRules.BASE_REPAIR_COST, InteractionRules.BASE_INCOME_PER_TURN,
                               d + " : reparer coute plus qu'un tour de plancher");
            }
            GameDifficulty.Current = DifficultyLevel.Medium;

            // Toute la Base ne se rachete pas en quelques clics.
            Assert.GreaterOrEqual(InteractionRules.BASE_HP / InteractionRules.BASE_REPAIR_AMOUNT, 8,
                                  "il faut au moins huit reparations pour refaire une Base entiere");

            // Hors partie, il n'y a pas de Base : la regle refuse sans rien facturer.
            Assert.IsFalse(InteractionRules.BaseNeedsRepair(), "sans Base, rien a reparer");
        }

        // =================================================================
        //  MOBILITE
        // =================================================================
        [Test]
        public void Posture_Assaut_Donne_Une_Case_De_Plus()
        {
            Assert.AreEqual(1, InteractionRules.ASSAULT_MOVE_BONUS, "l'Assaut donne une case de plus");
            Assert.AreEqual(1, InteractionRules.MOUNTAIN_MOVE_BONUS, "un Centre donne une case de plus");

            PawnController t1 = MakePawn(TypeOfPawn.unit, 1);
            PawnController t2 = MakePawn(TypeOfPawn.unit, 2);
            PawnController foe = MakePawn(TypeOfPawn.enemy, 2);

            t1.SetStance(PawnStance.Guard);
            Assert.AreEqual(1, InteractionRules.GetTankSteps(t1), "rang 1 en Garde : une case");
            t1.SetStance(PawnStance.Hunt);
            Assert.AreEqual(1, InteractionRules.GetTankSteps(t1), "rang 1 en Chasse : une case");

            // LE POINT DE LA PASSE : un rang 1 en Assaut traverse en quatre tours au lieu
            // de huit, donc il peut atteindre un Shofar pendant une fenetre de bouclier
            // rompu (5 a 6 tours selon la difficulte).
            t1.SetStance(PawnStance.Assault);
            Assert.AreEqual(2, InteractionRules.GetTankSteps(t1), "rang 1 en Assaut : deux cases");

            t2.SetStance(PawnStance.Assault);
            Assert.AreEqual(3, InteractionRules.GetTankSteps(t2), "rang 2 en Assaut : trois cases");

            foreach (DifficultyLevel d in new[] { DifficultyLevel.Easy, DifficultyLevel.Medium, DifficultyLevel.Hard })
            {
                GameDifficulty.Current = d;
                Assert.GreaterOrEqual(InteractionRules.GetTankSteps(t1) * InteractionRules.PORTAL_SHIELD_DOWN_TURNS, 8,
                    d + " : un rang 1 en Assaut couvre huit cases dans une fenetre de bouclier");
            }
            GameDifficulty.Current = DifficultyLevel.Medium;

            // Le bonus est au joueur seul : un ennemi garde la mobilite de son rang.
            Assert.AreEqual(1, InteractionRules.GetTankSteps(foe), "un ennemi n'a pas de bonus de posture");

            Kill(t1); Kill(t2); Kill(foe);
        }

        // =================================================================
        //  PLAFOND DE TANKS
        // =================================================================
        [Test]
        public void Plafond_De_Tanks_Par_Difficulte()
        {
            Assert.AreEqual(2, InteractionRules.TANK_CAP_PER_COMMAND_L1, "Centre rang 1 : +2 Tanks");
            Assert.AreEqual(4, InteractionRules.TANK_CAP_PER_COMMAND_L2, "Centre rang 2 : +4 Tanks");
            Assert.AreEqual(6, InteractionRules.TANK_CAP_FROM_COMMAND_MAX, "les Centres ajoutent +6 au plus, tous ensemble");

            // Le plafond ne peut pas s'envoler : six Centres n'en donnent pas douze.
            Assert.LessOrEqual(InteractionRules.TANK_CAP_FROM_COMMAND_MAX,
                               InteractionRules.TANK_CAP_PER_COMMAND_L2 * 2,
                               "deux Centres rang 2 suffisent a ouvrir le plafond en grand");

            foreach (DifficultyLevel d in new[] { DifficultyLevel.Easy, DifficultyLevel.Medium, DifficultyLevel.Hard })
            {
                GameDifficulty.Current = d;
                Assert.AreEqual(GameDifficulty.Pick(10, 9, 8), InteractionRules.TANK_CAP_BASE, d + " : la Base coordonne 10 / 9 / 8 Tanks");
                Assert.Greater(InteractionRules.TANK_CAP_BASE, InteractionRules.MAX_ACTIVE_ENEMIES - 1,
                               d + " : le joueur peut au moins egaler le plafond d'ennemis");
            }
            GameDifficulty.Current = DifficultyLevel.Medium;

            // Sans plateau charge, le plafond vaut la Base seule et personne n'est compte.
            Assert.AreEqual(InteractionRules.TANK_CAP_BASE, InteractionRules.TankCap(), "plafond sans Centre");
            Assert.AreEqual(0, InteractionRules.CountTanks(), "aucun Tank hors partie");
            Assert.IsTrue(InteractionRules.CanAddTank(), "une place libre hors partie");
        }

        // =================================================================
        //  SHOFARS
        // =================================================================
        [Test]
        public void Shofars_Instabilite_Et_Bouclier()
        {
            Assert.AreEqual(6, InteractionRules.PORTAL_COUNT);
            Assert.AreEqual(3, InteractionRules.PORTAL_EVOLVE_STAGGER);
            Assert.AreEqual(0.10f, InteractionRules.PORTAL_MINIBOSS_CHANCE, 0.0001f);

            foreach (DifficultyLevel d in new[] { DifficultyLevel.Easy, DifficultyLevel.Medium, DifficultyLevel.Hard })
            {
                GameDifficulty.Current = d;
                int needed = InteractionRules.PORTAL_KILLS_TO_BREAK_SHIELD;
                Assert.AreEqual(GameDifficulty.Pick(2, 3, 4), needed, d + " : 2 / 3 / 4 fissures pour rompre");
                Assert.AreEqual(GameDifficulty.Pick(6, 5, 5), InteractionRules.PORTAL_SHIELD_DOWN_TURNS, d + " : bouclier rompu 6 / 5 / 5 tours");
                Assert.AreEqual(GameDifficulty.Pick(6, 6, 8), InteractionRules.MAX_ACTIVE_ENEMIES, d + " : plafond d'ennemis 6 / 6 / 8");
                Assert.AreEqual(GameDifficulty.Pick(1, 2, 2), InteractionRules.PORTAL_SHIELD_DIVISOR, d + " : bouclier divise par 1 / 2 / 2");
                Assert.AreEqual(GameDifficulty.Pick(16, 14, 10), InteractionRules.PORTAL_KILL_BACKLASH, d + " : contrecoup 16 / 14 / 10");
                Assert.AreEqual(GameDifficulty.Pick(10, 15, 20), InteractionRules.PORTAL_BACKLASH_FLOOR_PERCENT, d + " : plancher 10 / 15 / 20 %");
                Assert.AreEqual(GameDifficulty.Pick(30, 18, 16), InteractionRules.PORTAL_EVOLVE_AFTER_TURNS, d + " : rang 2 des Shofars au tour 30 / 18 / 16");
                Assert.AreEqual(GameDifficulty.Pick(40, 50, 50), InteractionRules.PORTAL_HP_L1, d + " : Shofar rang 1 a 40 / 50 / 50 PV");
                Assert.AreEqual(GameDifficulty.Pick(5, 3, 3), InteractionRules.PORTAL_CALM_TURNS, d + " : calme avant regeneration 5 / 3 / 3");
                Assert.AreEqual(GameDifficulty.Pick(2, 2, 3), InteractionRules.MAX_SPAWNS_PER_TURN, d + " : deploiements par tour 2 / 2 / 3");
                Assert.GreaterOrEqual(InteractionRules.PORTAL_SPAWN_INTERVAL, GameDifficulty.Pick(4, 5, 5) , d + " : intervalle de deploiement (hors acceleration)");

                int floor = (InteractionRules.PORTAL_HP_L1 * InteractionRules.PORTAL_BACKLASH_FLOOR_PERCENT) / 100;
                Assert.Greater(floor, 0, d + " : le contrecoup n'acheve jamais un Shofar");
                int backlash = InteractionRules.PORTAL_KILL_BACKLASH * needed;
                Assert.GreaterOrEqual(backlash, (InteractionRules.PORTAL_HP_L1 - floor) / 2,
                                      d + " : tenir la ligne enleve au moins la moitie de ce qu'un Shofar rang 1 peut perdre");
            }

            // Sans PortalManager (hors partie), le filtre rend les degats tels quels.
            GameObject go = new GameObject("TestPortal");
            Hexagon portal = go.AddComponent<Hexagon>();
            portal.type = TypeOfHex.portal;
            portal.positionInTheBoard = new HexCoord(7, -7, 0);
            Assert.AreEqual(10, InteractionRules.FilterPortalDamage(portal, 10), "hors partie : pas de bouclier a appliquer");
            Object.DestroyImmediate(go);
        }

        // =================================================================
        //  DISTANCES ET PLATEAU
        // =================================================================
        [Test]
        public void Distance_Hexagonale()
        {
            HexCoord o = new HexCoord(0, 0, 0);
            Assert.AreEqual(0, BoardController.GetHexDistance(o, o));
            Assert.AreEqual(1, BoardController.GetHexDistance(o, new HexCoord(1, -1, 0)));
            Assert.AreEqual(1, BoardController.GetHexDistance(o, new HexCoord(0, 1, -1)));
            Assert.AreEqual(2, BoardController.GetHexDistance(o, new HexCoord(2, -1, -1)));
            Assert.AreEqual(7, BoardController.GetHexDistance(o, new HexCoord(7, -7, 0)), "un coin est a 7 du centre");
            Assert.AreEqual(14, BoardController.GetHexDistance(new HexCoord(7, -7, 0), new HexCoord(-7, 7, 0)), "deux coins opposes");
            Assert.AreEqual(BoardController.GetHexDistance(o, new HexCoord(3, -1, -2)),
                            BoardController.GetHexDistance(new HexCoord(3, -1, -2), o), "symetrique");
        }

        [Test]
        public void HexCoord_Comparaison()
        {
            HexCoord a = new HexCoord(2, -3, 1);
            Assert.IsTrue(a.CompareHexCoord(new HexCoord(2, -3, 1)));
            Assert.IsFalse(a.CompareHexCoord(new HexCoord(2, -2, 0)));
            Assert.AreEqual(0, a.q + a.r + a.s, "q + r + s = 0");
        }

        [Test]
        public void Dispositions_Du_Plateau()
        {
            Assert.AreEqual(4, MapGenerator.LayoutCount);
            Assert.AreEqual(MapGenerator.LayoutCount, MapGenerator.LayoutNameKeys.Length);
            Assert.AreEqual(MapGenerator.LayoutCount, MapGenerator.LayoutBodyKeys.Length);
            for (int i = 0; i < MapGenerator.LayoutCount; i++)
                Assert.IsTrue(MapGenerator.LayoutIsSound(i), "disposition " + i + " : voisinage et distance aux Shofars");

            int before = MapGenerator.CurrentLayout;
            MapGenerator.CurrentLayout = 99;
            Assert.AreEqual(0, MapGenerator.CurrentLayout, "hors bornes : retombe sur 0");
            MapGenerator.CurrentLayout = -1;
            Assert.AreEqual(0, MapGenerator.CurrentLayout);
            MapGenerator.CurrentLayout = before;
        }

        /// <summary>
        /// AUCUN SHOFAR ENFERME, AUCUNE BASE INACCESSIBLE. Sur chaque disposition, un
        /// ennemi qui sort de n'importe quel Shofar doit pouvoir marcher jusqu'a la
        /// Base en evitant collines, Shofars et Base (les cases que IsHexWalkable
        /// interdit sur un plateau vierge). Sinon des ennemis restent coinces a vie
        /// dans leur coin - et le joueur ne peut pas non plus aller fermer ce Shofar.
        /// </summary>
        [Test]
        public void Chaque_Shofar_Peut_Atteindre_La_Base()
        {
            int before = MapGenerator.CurrentLayout;
            try
            {
                for (int layout = 0; layout < MapGenerator.LayoutCount; layout++)
                {
                    MapGenerator.CurrentLayout = layout;
                    var hexes = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.List<HexagonData>>(
                        MapGenerator.GenerateIntelligentBoard());

                    var type = new System.Collections.Generic.Dictionary<long, TypeOfHex>();
                    var portals = new System.Collections.Generic.List<HexCoord>();
                    var bases = new System.Collections.Generic.List<HexCoord>();
                    for (int i = 0; i < hexes.Count; i++)
                    {
                        type[Key(hexes[i].from)] = hexes[i].typeID;
                        if (hexes[i].typeID == TypeOfHex.portal) portals.Add(hexes[i].from);
                        if (hexes[i].typeID == TypeOfHex.Base) bases.Add(hexes[i].from);
                    }

                    // Les cases qu'il faut atteindre : celles qui touchent la Base.
                    var goal = new System.Collections.Generic.HashSet<long>();
                    for (int b = 0; b < bases.Count; b++)
                        foreach (HexCoord n in Neighbours(bases[b]))
                            if (Walkable(type, n)) goal.Add(Key(n));
                    Assert.Greater(goal.Count, 0, "disposition " + layout + " : aucune case praticable autour de la Base");

                    for (int p = 0; p < portals.Count; p++)
                    {
                        var seen = new System.Collections.Generic.HashSet<long>();
                        var queue = new System.Collections.Generic.Queue<HexCoord>();
                        int exits = 0;
                        foreach (HexCoord n in Neighbours(portals[p]))
                        {
                            if (!Walkable(type, n)) continue;
                            exits++;
                            if (seen.Add(Key(n))) queue.Enqueue(n);
                        }
                        Assert.Greater(exits, 0, "disposition " + layout + " : le Shofar (" + portals[p].q + "," + portals[p].r + ") n'a aucune case de sortie");

                        bool reached = false;
                        while (queue.Count > 0 && !reached)
                        {
                            HexCoord c = queue.Dequeue();
                            if (goal.Contains(Key(c))) { reached = true; break; }
                            foreach (HexCoord n in Neighbours(c))
                                if (Walkable(type, n) && seen.Add(Key(n))) queue.Enqueue(n);
                        }
                        Assert.IsTrue(reached, "disposition " + layout + " : aucun chemin du Shofar (" + portals[p].q + "," + portals[p].r + ") a la Base");
                    }
                }
            }
            finally
            {
                MapGenerator.CurrentLayout = before;
            }
        }

        private static long Key(HexCoord c) { return (long)(c.q + 100) * 1000 + (c.r + 100); }

        private static bool Walkable(System.Collections.Generic.Dictionary<long, TypeOfHex> type, HexCoord c)
        {
            TypeOfHex t;
            if (!type.TryGetValue(Key(c), out t)) return false;
            // Plateau vierge : rien n'est encore construit, seuls collines, Shofars et Base bloquent.
            return t != TypeOfHex.hill && t != TypeOfHex.portal && t != TypeOfHex.Base;
        }

        private static System.Collections.Generic.IEnumerable<HexCoord> Neighbours(HexCoord c)
        {
            int[] dq = { 1, 1, 0, -1, -1, 0 };
            int[] dr = { 0, -1, -1, 0, 1, 1 };
            for (int i = 0; i < 6; i++)
            {
                int q = c.q + dq[i], r = c.r + dr[i];
                yield return new HexCoord(q, r, -q - r);
            }
        }

        [Test]
        public void Chaque_Disposition_Genere_Un_Plateau_Complet()
        {
            int before = MapGenerator.CurrentLayout;
            try
            {
                for (int layout = 0; layout < MapGenerator.LayoutCount; layout++)
                {
                    MapGenerator.CurrentLayout = layout;
                    string json = MapGenerator.GenerateIntelligentBoard();
                    Assert.IsFalse(string.IsNullOrEmpty(json), "disposition " + layout + " : JSON vide");

                    var hexes = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.List<HexagonData>>(json);
                    Assert.IsNotNull(hexes);

                    int radius = MapGenerator.BoardRadius;
                    int expected = 3 * radius * (radius + 1) + 1;   // 169 pour un rayon de 7
                    Assert.AreEqual(expected, hexes.Count, "disposition " + layout + " : nombre de cases");

                    int portals = 0, bases = 0, gas = 0, crystal = 0, hill = 0, mountain = 0;
                    var seen = new System.Collections.Generic.HashSet<int>();
                    for (int i = 0; i < hexes.Count; i++)
                    {
                        HexagonData h = hexes[i];
                        Assert.IsNotNull(h.from, "case sans coordonnee");
                        Assert.AreEqual(0, h.from.q + h.from.r + h.from.s, "coordonnee cubique invalide");
                        Assert.IsTrue(seen.Add(h.from.q * 1000 + h.from.r), "coordonnee en double (" + h.from.q + "," + h.from.r + ")");
                        Assert.LessOrEqual(BoardController.GetHexDistance(new HexCoord(0, 0, 0), h.from), radius, "case hors du rayon");
                        Assert.GreaterOrEqual(h.energy, 0, "PV negatifs");

                        switch (h.typeID)
                        {
                            case TypeOfHex.portal: portals++; Assert.AreEqual(InteractionRules.PORTAL_HP_L1, h.energy, "PV d'un Shofar neuf"); break;
                            case TypeOfHex.Base: bases++; break;
                            case TypeOfHex.gas: gas++; break;
                            case TypeOfHex.crystal: crystal++; break;
                            case TypeOfHex.hill: hill++; break;
                            case TypeOfHex.mountain: mountain++; break;
                            case TypeOfHex.unit:
                            case TypeOfHex.enemy:
                                Assert.Fail("disposition " + layout + " : un pion pose d'avance");
                                break;
                        }
                    }

                    Assert.AreEqual(InteractionRules.PORTAL_COUNT, portals, "disposition " + layout + " : six Shofars");
                    Assert.Greater(bases, 0, "disposition " + layout + " : une Base");
                    Assert.Greater(gas, 0, "disposition " + layout + " : du gaz");
                    Assert.Greater(crystal, 0, "disposition " + layout + " : du cristal");
                    Assert.Greater(hill, 0, "disposition " + layout + " : des collines");
                    Assert.Greater(mountain, 0, "disposition " + layout + " : des montagnes");
                }
            }
            finally
            {
                MapGenerator.CurrentLayout = before;
            }
        }
    }
}
#endif
