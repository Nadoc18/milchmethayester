using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using MNLTHII;
using MNLTHII.Rules;

namespace MNLTHII.Managers
{
    /// <summary>Etape courante du tour, pour l'UI et pour filtrer les clics.</summary>
    public enum TurnPhase
    {
        Idle,
        Income,     // le Trivia est ouvert, le plateau est verrouille
        Spending,   // le joueur depense librement son Energie
        Tanks,
        Enemies,
        EndOfTurn
    }

    /// <summary>
    /// Sequenceur de tour.
    ///
    /// ANCIENNE BOUCLE (V3 brute) :
    ///   le joueur cliquait un hexagone, repondait a une question, et le tour
    ///   s'arretait la. Une decision par tour, gratuite : aucun arbitrage possible.
    ///
    /// BOUCLE ACTUELLE :
    ///   1. REVENU    : revenu passif (Base + Cristaux), puis une question de Trivia.
    ///                  Bonne reponse = +25 Energie. C'est tout ce que fait le Trivia.
    ///   2. DEPENSE   : phase libre. Le joueur clique autant d'hexagones que son solde
    ///                  le permet : creer un Tank, batir, faire evoluer, ou changer la
    ///                  posture d'un Tank (gratuit). Il termine avec Espace, Entree ou
    ///                  le bouton de fin de tour.
    ///   3. TANKS     : les Tanks agissent seuls, selon leur posture.
    ///   4. YETZER HARA : EnemyAI joue les ennemis.
    ///   5. FIN DE TOUR : batiments, portails, surge annoncee, victoire / defaite.
    ///
    /// Le revenu est ainsi separe de la depense : c'est ce qui cree un budget, donc
    /// des choix, donc des strategies differentes d'une partie a l'autre.
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        public static TurnManager Instance { get; private set; }

        [Header("UI References")]
        public TMPro.TextMeshProUGUI turnNumberText;
        [Tooltip("Optionnel : affiche la phase courante et le rappel de la touche de fin de tour.")]
        public TMPro.TextMeshProUGUI phaseText;

        [Header("Turn State")]
        public int currentTurn = 1;
        public bool isPlayerTurn = true;
        public TurnPhase phase = TurnPhase.Idle;

        [Tooltip("Duree maximale de la phase de depense, en secondes. 0 = illimitee.")]
        public float spendingDuration = 0f;
        public float turnTimer = 0f;

        [Header("Fin de partie")]
        public bool gameOver = false;

        [Header("Ciblage des Tanks (equilibrage)")]
        [Tooltip("Attrait de base d'un Portail : c'est lui qui fait gagner la partie.")]
        public int portalPriority = 900;
        [Tooltip("Attrait de base d'une unite ennemie.")]
        public int enemyPriority = 620;
        [Tooltip("Malus par case de distance, appliquer a toutes les cibles.")]
        public int distanceWeight = 35;
        [Tooltip("Bonus par case dont l'ennemi est plus proche de la Base que le rayon d'alerte.")]
        public int threatWeight = 90;
        [Tooltip("Rayon d'alerte autour de la Base, en cases.")]
        public int threatRadius = 6;
        [Tooltip("Bonus si la cible est deja a portee : l'attaque ne coute aucun deplacement.")]
        public int inRangeBonus = 200;
        [Tooltip("Bonus si le coup suffit a detruire la cible ce tour-ci.")]
        public int finishBonus = 260;
        [Tooltip("Bonus supplementaire pour un Portail deja passe au Niveau 2.")]
        public int portalLevel2Bonus = 150;
        [Tooltip("Bonus pour un ennemi a distance (portee 2 ou 3), plus dangereux.")]
        public int rangedEnemyBonus = 80;
        [Tooltip("Bonus quand le bouclier du Portail est tombe : c'est la fenetre d'assaut.")]
        public int portalShieldDownBonus = 500;
        [Tooltip("Legitime defense : en Assaut, un ennemi deja a portee passe avant le Portail.")]
        public int assaultSelfDefenseBonus = 1100;

        [Header("Postures")]
        [Tooltip("Distance maximale a la Base qu'un Tank en Garde accepte de franchir.")]
        public int guardRadius = 4;

        [Header("Debug")]
        [Tooltip("Serialise l'etat complet du plateau a chaque fin de tour. Couteux : reserve au debug.")]
        public bool logStateEachTurn = false;

        public event Action<int> OnTurnStarted;
        public event Action<int> OnTurnEnded;

        private bool _gameStarted = false;

        // Tampon reutilise : FindAll(lambda) allouait une List et une closure par tour.
        private readonly List<PawnController> _tankBuffer = new List<PawnController>(24);

        // Instances partagees : chaque "new WaitForSeconds" dans une coroutine alloue.
        // =================================================================
        //  RYTHME DES PHASES AUTOMATIQUES
        // =================================================================
        /// <summary>
        /// Ces cinq attentes decident si le joueur COMPREND la phase ou la subit.
        ///
        /// Le defaut precedent etait trop court, et pour une raison precise : le temps
        /// de focalisation valait 0.35 s alors que la camera met 0.45 s a rejoindre sa
        /// cible. L'attaque se resolvait donc pendant que la camera voyageait encore -
        /// le joueur arrivait apres le coup. Le temps de focalisation doit toujours
        /// rester SUPERIEUR a CameraDirector.moveDuration, sinon on filme un plan
        /// qu'on a deja quitte.
        ///
        /// Le temps de resolution, lui, doit couvrir la vie du chiffre de degats
        /// (environ une seconde) : le raccourcir revient a effacer le nombre avant
        /// qu'il soit lu.
        ///
        /// Tout est expose ici pour que le rythme se regle manette en main, sans
        /// recompiler.
        /// </summary>
        [Header("Rythme des phases automatiques")]
        [Tooltip("Temps sur l'unite avant qu'elle agisse. DOIT depasser la duree de deplacement de la camera.")]
        [Header("Revenu des usines")]
        /// <summary>
        /// Ton particle effect, s'il y en a un. Laisse vide : l'effet de buff deja
        /// present dans le projet prend le relais, donc rien ne bloque.
        /// </summary>
        public GameObject gasIncomeFX;
        public float gasIncomeFXLifetime = 2f;

        /// <summary>Temps entre deux usines. Une a la fois, sinon on ne compte rien.</summary>
        public float factoryIncomeDelay = 0.85f;

        /// <summary>Duree de la montee du compteur pour UNE usine.</summary>
        public float factoryCountDuration = 0.55f;

        [Tooltip("La camera va voir chaque usine qui paie.")]
        public bool focusFactories = true;

        private WaitForSeconds _waitFactory;
        private float _cachedFactory = -1f;

        public float focusDelay = 0.95f;

        [Tooltip("Temps de visee, entre l'orientation et le coup.")]
        public float aimDelay = 0.35f;

        [Tooltip("Temps apres le coup. Doit couvrir la vie du chiffre de degats.")]
        public float resolveDelay = 1.05f;

        [Tooltip("Duree d'un pas de deplacement.")]
        public float moveDelay = 0.85f;

        [Tooltip("Respiration entre deux unites.")]
        public float stepDelay = 0.55f;

        // Instances reconstruites quand un reglage change : WaitForSeconds est
        // immuable, et en allouer une a chaque yield ferait travailler le
        // ramasse-miettes au milieu de l'action.
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


        public bool IsSpendingPhase { get { return phase == TurnPhase.Spending && !gameOver; } }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(this); return; }
        }

        // Le demarrage est pilote par LocalGameEngine, une fois la carte construite.

        private void Update()
        {
            if (gameOver || !_gameStarted) return;
            if (phase != TurnPhase.Spending) return;

            // Fin de tour manuelle : c'est le joueur qui decide quand il a fini
            // de depenser, pas le jeu.
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                EndPlayerTurn();
                return;
            }

            if (spendingDuration <= 0f) return;

            turnTimer -= Time.deltaTime;
            if (turnTimer <= 0f) EndPlayerTurn();
        }

        // =================================================================
        //  1. DEBUT DE TOUR -> PHASE DE REVENU
        // =================================================================
        public void StartTurn()
        {
            if (gameOver) return;
            _gameStarted = true;
            StartCoroutine(StartTurnRoutine());
        }

        private IEnumerator StartTurnRoutine()
        {
            Debug.Log("[TurnManager] --- Debut du tour " + currentTurn + " ---");

            isPlayerTurn = true;
            phase = TurnPhase.Income;

            // SetText avec argument formate : contrairement a l'affectation de .text
            // avec une concatenation, TMP ecrit directement dans son buffer interne
            // et n'alloue aucune string.
            if (turnNumberText != null) turnNumberText.SetText("TOUR {0}", currentTurn);
            if (GameManager.instance != null)
            {
                GameManager.instance.turn = currentTurn;
                // Plateau verrouille tant que la question est a l'ecran.
                GameManager.instance.canClickOrHover = false;
            }
            if (BoardController.instance != null) BoardController.instance.RefreshAllAuras();

            if (OnTurnStarted != null) OnTurnStarted.Invoke(currentTurn);

            yield return null;

            // Revenu du tour : le plancher de la Base, plus chaque usine debout.
            //
            // Il n'est PLUS verse d'un bloc. GrantPassiveIncome ne donne que le plancher
            // de la Base ; chaque usine verse sa part a son tour, pendant qu'on la
            // regarde. Sans ca le compteur avait deja tout encaisse avant que la
            // premiere usine ne s'allume, et l'animation ne montrait rien du tout.
            int passive = InteractionRules.GrantBaseIncome();
            passive += ShowFactoryIncomeTotal();

            Debug.LogFormat("[Economie] Revenu du tour : +{0} Energie.", passive);

            yield return StartCoroutine(ShowFactoryIncome());

            // --- 1. L'ouverture du tour ---
            // Un tour doit COMMENCER quelque part, visiblement. Sans cet ecran la
            // partie est un flux continu ou le joueur ne sait plus s'il vient de
            // jouer ou s'il regarde le Yetzer Hara jouer. C'est aussi le seul endroit
            // ou le revenu passif est annonce comme un evenement, au lieu de faire
            // monter un compteur en silence dans un coin de l'ecran.
            if (TurnAnnouncePanel.Instance != null)
                yield return StartCoroutine(TurnAnnouncePanel.Instance.Play(currentTurn, passive));

            // --- 2. L'enseignement ---
            //
            // Ce qui remplace la question de Trivia. La question etait un peage : il
            // fallait payer pour avoir le droit de jouer, chaque tour, et l'Energie
            // qu'elle versait ne se trouvait nulle part sur la carte. Maintenant
            // l'Energie vient des usines, et cette etape sert a apprendre quelque chose
            // sur ce qu'on affronte.
            //
            // Contrairement au Trivia, on ATTEND ici la fin de la sequence au lieu de
            // se faire rappeler : l'ecran ne rend la main que quand tout a ete lu, et
            // c'est plus simple a suivre qu'un aller-retour par un autre manager.
            if (TeachingPanel.Instance != null)
            {
                yield return StartCoroutine(TeachingPanel.Instance.Play(currentTurn));
                OpenSpendingPhase();
            }
            else
            {
                Debug.LogError("[TurnManager] ETAPE D'ENSEIGNEMENT SAUTEE : aucun TeachingPanel dans "
                             + "la scene. Relance Milchemet > Construire le HUD, et verifie que le "
                             + "Canvas est ACTIF - un composant sur un objet desactive ne recoit "
                             + "jamais son Awake, donc son Instance reste nulle.");
                OpenSpendingPhase();
            }
        }

        /// <summary>
        /// Les usines s'allument l'une apres l'autre, avec leur gain qui monte.
        ///
        /// POURQUOI CETTE PETITE CEREMONIE
        ///
        /// Un compteur d'Energie qui saute de 40 a 70 en silence n'apprend rien. Le
        /// joueur doit voir QUELLE usine paie, et surtout remarquer le tour ou l'une
        /// d'elles ne repond plus - parce qu'elle est tombee pendant la nuit. C'est
        /// toute la difference entre une economie et un chiffre.
        ///
        /// Note d'optimisation : aucune liste n'est construite, on parcourt celle du
        /// plateau. Les attentes sont mises en cache dans _waitFactory.
        /// </summary>
        /// <summary>Total que les usines vont verser, pour le journal et l'annonce.</summary>
        private int ShowFactoryIncomeTotal()
        {
            if (BoardController.instance == null) return 0;

            List<Hexagon> hexes = BoardController.instance.HexagonsInBoard;
            if (hexes == null) return 0;

            int total = 0;
            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex == null || hex.type != TypeOfHex.gas) continue;
                if (hex.level < 1 || hex.currentHP <= 0) continue;
                total += InteractionRules.GetGasIncome(hex.level);
            }
            return total;
        }

        private IEnumerator ShowFactoryIncome()
        {
            if (BoardController.instance == null) yield break;

            List<Hexagon> hexes = BoardController.instance.HexagonsInBoard;
            if (hexes == null) yield break;

            RefreshFactoryWait();

            FXManager fx = FXManager.Instance;
            EnergyManager wallet = EnergyManager.Instance;
            HudController hud = HudController.Instance;

            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex == null || hex.type != TypeOfHex.gas) continue;
                if (hex.level < 1 || hex.currentHP <= 0) continue;

                int amount = InteractionRules.GetGasIncome(hex.level);
                if (amount <= 0) continue;

                Vector3 spot = hex.transform.position;

                // On REGARDE l'usine qui paie. C'est ce qui relie le chiffre en haut de
                // l'ecran a un endroit precis du plateau - et c'est ce qui rendra
                // evident, le jour ou elle tombera, qu'il manque quelque chose.
                if (focusFactories) CameraDirector.FocusPoint(spot);

                if (gasIncomeFX != null)
                {
                    GameObject burst = Instantiate(gasIncomeFX, spot + Vector3.up * 0.4f, Quaternion.identity);
                    Destroy(burst, gasIncomeFXLifetime);
                }
                else if (fx != null)
                {
                    fx.SpawnEnergyBuffFX(spot);
                }

                MNLTHII.UI.DamagePopup.Show(spot, amount, MNLTHII.UI.DamageKind.Income);

                // L'Energie est versee MAINTENANT, et le compteur monte jusqu'a sa
                // nouvelle valeur pendant qu'on regarde cette usine-la.
                if (wallet != null)
                {
                    wallet.Add(amount);
                    if (hud != null) hud.CountEnergyTo(wallet.CurrentEnergy, factoryCountDuration);
                }

                yield return _waitFactory;
            }

            if (focusFactories) CameraDirector.ReleaseCamera();
        }

        private void RefreshFactoryWait()
        {
            if (_cachedFactory == factoryIncomeDelay && _waitFactory != null) return;

            _cachedFactory = factoryIncomeDelay;
            _waitFactory = new WaitForSeconds(factoryIncomeDelay);
        }

        // =================================================================
        //  2. PHASE DE DEPENSE LIBRE
        // =================================================================
        /// <summary>Appele par TriviaManager une fois la question resolue.</summary>
        public void OpenSpendingPhase()
        {
            if (gameOver) return;

            phase = TurnPhase.Spending;
            isPlayerTurn = true;
            turnTimer = spendingDuration;

            if (GameManager.instance != null) GameManager.instance.canClickOrHover = true;

            int energy = (EnergyManager.Instance != null) ? EnergyManager.Instance.CurrentEnergy : 0;
            if (phaseText != null) phaseText.SetText("DEPENSE - {0} Energie - ESPACE pour finir", energy);

            // Le conseiller ne parle QUE pendant la phase de depense : c'est le seul
            // moment ou le joueur peut agir, donc le seul ou un conseil sert a quelque
            // chose. Pendant les phases jouees, il se tait.
            if (AdvisorPanel.Instance != null) AdvisorPanel.Instance.Refresh();

            Debug.LogFormat("[TurnManager] Phase de depense. Solde : {0} Energie. Espace pour terminer, Tab pour voir la menace.", energy);
        }

        /// <summary>
        /// Un clic du joueur pendant la phase de depense. L'action est facturee par
        /// InteractionRules ; le tour ne s'arrete PAS, contrairement a l'ancienne boucle.
        /// </summary>
        public TriviaOutcome HandleSpendingClick(HexCoord coord)
        {
            if (!IsSpendingPhase) return TriviaOutcome.Blocked_NotPlayerPhase;
            if (BoardController.instance == null) return TriviaOutcome.Blocked_NotPlayerPhase;

            // Tant que l'ecran de choix est ouvert, le plateau ne repond plus : sinon
            // un clic a cote creerait un deuxieme Tank pendant qu'on choisit la
            // posture du premier, et la carte en attente ne voudrait plus rien dire.
            TankChoicePanel choice = TankChoicePanel.Instance;
            if (choice != null && choice.IsOpen) return TriviaOutcome.EnergyOnly;

            Hexagon hex = BoardController.instance.getHexByCoord(coord);
            if (hex == null) return TriviaOutcome.EnergyOnly;

            // Un clic qui concerne un Tank - en creer un, ou modifier celui qui est
            // deja pose - ouvre l'ecran de choix au lieu d'agir immediatement. Le
            // resultat arrivera plus tard, dans ApplyTankChoice.
            //
            // Rendre la main sans avoir agi ne casse rien : le seul appelant,
            // LocalGameEngine.ProcessHexClick, ignore la valeur de retour.
            if (TryOpenTankChoice(hex, choice)) return TriviaOutcome.EnergyOnly;

            return FinishSpendingAction(InteractionRules.ApplyPlayerAction(hex));
        }

        /// <summary>
        /// Le apres-coup commun a toutes les actions de la phase de depense : mettre
        /// le solde a jour, oublier l'hexagone clique, et rafraichir l'apercu de menace
        /// parce que poser un Bunker ou un Tank change ce que l'ennemi va faire.
        /// </summary>
        private TriviaOutcome FinishSpendingAction(TriviaOutcome outcome)
        {
            int energy = (EnergyManager.Instance != null) ? EnergyManager.Instance.CurrentEnergy : 0;
            if (phaseText != null) phaseText.SetText("DEPENSE - {0} Energie - ESPACE pour finir", energy);

            if (GameManager.instance != null) GameManager.instance.ClearHexClicked();

            if (ThreatPreview.Instance != null) ThreatPreview.Instance.RefreshIfVisible();

            // Une action change le plateau, donc elle change ce qu'il est urgent de
            // faire ensuite. Un conseiller qui garde les conseils du debut de phase
            // proposerait de construire un Tank sur une case qui vient d'en recevoir un.
            if (AdvisorPanel.Instance != null) AdvisorPanel.Instance.Refresh();

            Debug.LogFormat("[TurnManager] Action : {0}. Solde : {1}.", outcome, energy);
            return outcome;
        }

        /// <summary>
        /// Le joueur vient d'accepter un conseil. Meme apres-coup qu'un clic sur le
        /// plateau : l'action est deja faite et facturee par AdvisorPanel.
        /// </summary>
        public void NotifyAdvisorAction(TriviaOutcome outcome)
        {
            FinishSpendingAction(outcome);
        }

        // -----------------------------------------------------------------
        //  L'ECRAN DE CHOIX D'UN TANK
        // -----------------------------------------------------------------
        //
        // POURQUOI CET ECRAN EXISTE
        //
        // Avant, un clic sur une case libre posait un Tank en Garde, et un clic sur un
        // Tank faisait tourner sa posture sans prevenir - ou, si un Cristal etait a
        // portee et le solde suffisant, le faisait evoluer a la place. Le joueur ne
        // savait donc ni ce qu'il allait obtenir, ni ce que chaque posture FAIT.
        // C'est ce qui produisait une armee entiere de Tanks en Garde.
        //
        // Desormais le clic ouvre un ecran qui montre les trois postures cote a cote
        // avec leur role, et l'evolution comme quatrieme carte. L'evolution ne vole
        // donc plus le clic : elle se choisit.
        //
        // Ces trois champs ne sont pas un etat de jeu, seulement la memoire du clic en
        // cours. Ils sont remis a null des que le choix est applique ou annule.
        private Hexagon _pendingTankHex;
        private PawnController _pendingTank;
        private Action<int> _tankChoiceHandler;

        /// <summary>
        /// Ouvre l'ecran de choix si ce clic concerne un Tank, et dit s'il l'a ouvert.
        /// Faux signifie "traite ce clic comme avant" : pas de HUD dans la scene, case
        /// qui n'est pas une usine a Tanks, ou solde trop faible pour en creer un.
        /// </summary>
        private bool TryOpenTankChoice(Hexagon hex, TankChoicePanel panel)
        {
            if (panel == null) return false;

            int energy = (EnergyManager.Instance != null) ? EnergyManager.Instance.CurrentEnergy : 0;

            PawnController occupant = BoardController.instance.getPawnByCoord(hex.positionInTheBoard);

            // --- un Tank a nous : changer sa posture, ou le faire evoluer ---
            if (occupant != null)
            {
                // Seules les unites portent une posture. Un ennemi, un Bunker ou une
                // Prod gardent l'ancien chemin, qui sait deja quoi en faire.
                if (occupant.IsEnemy || occupant.typeOfPawn != TypeOfPawn.unit) return false;

                // Une evolution impossible - deja au Niveau 2, ou aucun Cristal a
                // portee - passe 0 : la quatrieme carte est alors masquee au lieu
                // d'etre grisee sans explication.
                int evolveCost = 0;
                if (occupant.level < InteractionRules.MAX_TANK_LEVEL
                    && InteractionRules.HasCrystalSupport(occupant.hexcoord))
                    evolveCost = InteractionRules.TANK_EVOLVE_COST;

                _pendingTankHex = null;
                _pendingTank = occupant;

                if (_tankChoiceHandler == null) _tankChoiceHandler = ApplyTankChoice;
                panel.SetChoiceHandler(_tankChoiceHandler);
                panel.Show(false, InteractionRules.TANK_STANCE_COST,
                           (int)occupant.stance, evolveCost, energy);
                return true;
            }

            // --- une case libre ou l'on peut poser un Tank ---
            if (hex.type != TypeOfHex.plain && hex.type != TypeOfHex.desert) return false;

            // Hors budget, on n'ouvre rien : un ecran dont les quatre cartes sont
            // eteintes n'apprend rien. L'ancien chemin renvoie Blocked_NotEnoughEnergy,
            // et c'est ce message-la qui est utile.
            if (energy < InteractionRules.TANK_CREATION_COST) return false;

            _pendingTank = null;
            _pendingTankHex = hex;

            if (_tankChoiceHandler == null) _tankChoiceHandler = ApplyTankChoice;
            panel.SetChoiceHandler(_tankChoiceHandler);

            // currentStance vaut -1 : a la creation aucune posture n'est encore
            // "celle du Tank", donc aucune carte n'est barree. Le prix affiche est
            // celui du Tank lui-meme ; la posture, elle, est gratuite a la pose.
            panel.Show(true, InteractionRules.TANK_CREATION_COST, -1, 0, energy);
            return true;
        }

        /// <summary>
        /// Reponse de l'ecran : 0 a 2 pour une posture, 3 pour l'evolution, -1 pour une
        /// annulation.
        /// </summary>
        private void ApplyTankChoice(int slot)
        {
            Hexagon hex = _pendingTankHex;
            PawnController tank = _pendingTank;

            // Oublie AVANT d'agir : meme si l'action echoue, ce clic-la est fini.
            _pendingTankHex = null;
            _pendingTank = null;

            if (slot < 0) return;

            // La phase a pu se terminer pendant que l'ecran etait ouvert - l'espace
            // reste actif. Facturer maintenant donnerait un Tank paye hors de son tour.
            if (!IsSpendingPhase) return;

            TriviaOutcome outcome;

            if (tank != null)
            {
                if (slot >= UI.StanceStyle.Count)
                    outcome = InteractionRules.UpgradeTank(tank);
                else
                    outcome = InteractionRules.ChangeTankStance(tank, (PawnStance)slot);
            }
            else if (hex != null && slot < UI.StanceStyle.Count)
            {
                outcome = InteractionRules.CreateTankWithStance(hex, (PawnStance)slot);
            }
            else return;

            FinishSpendingAction(outcome);
        }

        /// <summary>Referme l'ecran de choix s'il etait reste ouvert.</summary>
        private void CloseTankChoice()
        {
            _pendingTankHex = null;
            _pendingTank = null;

            if (TankChoicePanel.Instance != null) TankChoicePanel.Instance.Hide();
        }

        // =================================================================
        //  3. FIN DE LA PHASE JOUEUR -> PHASE DES TANKS
        // =================================================================
        public void EndPlayerTurn()
        {
            if (gameOver) return;
            if (phase != TurnPhase.Spending && phase != TurnPhase.Income) return;

            isPlayerTurn = false;
            phase = TurnPhase.Tanks;
            turnTimer = 0;

            // Un ecran de choix reste ouvert appartiendrait au tour precedent : il
            // masquerait la phase des Tanks, et son bouton facturerait un Tank en
            // dehors du tour ou il a ete demande.
            CloseTankChoice();

            if (AdvisorPanel.Instance != null) AdvisorPanel.Instance.Hide();

            if (GameManager.instance != null)
            {
                GameManager.instance.canClickOrHover = false;
                GameManager.instance.ClearHexClicked();
            }
            if (phaseText != null) phaseText.SetText("TANKS");

            // L'apercu se referme des que la phase commence : ce n'est plus une
            // prevision, c'est en train d'arriver.
            if (ThreatPreview.Instance != null) ThreatPreview.Instance.Hide();

            StartCoroutine(PlayerUnitsPhase());
        }

        /// <summary>Compatibilite : ancien point d'entree de la phase des Tanks.</summary>
        public void ProcessPlayerUnits() { StartCoroutine(PlayerUnitsPhase()); }

        private IEnumerator PlayerUnitsPhase()
        {
            yield return StartCoroutine(ProcessPlayerUnitsSequential());

            if (gameOver) yield break;

            phase = TurnPhase.Enemies;
            if (phaseText != null) phaseText.SetText("YETZER HARA");

            // Passe la main a l'IA (EnemyAI ecoute cet evenement).
            if (OnTurnEnded != null) OnTurnEnded.Invoke(currentTurn);
            else StartNextTurn();
        }

        private IEnumerator ProcessPlayerUnitsSequential()
        {
            RefreshPhaseWaits();

            BoardController board = BoardController.instance;
            if (board == null || board.PawnsInBoard == null) yield break;

            // Copie a plat dans un tampon membre : la liste du plateau est modifiee
            // pendant la phase (morts, spawns), on ne peut pas l'iterer directement.
            _tankBuffer.Clear();
            List<PawnController> pawns = board.PawnsInBoard;
            for (int i = 0; i < pawns.Count; i++)
            {
                PawnController p = pawns[i];
                if (p != null && !p.IsEnemy) _tankBuffer.Add(p);
            }

            for (int i = 0; i < _tankBuffer.Count; i++)
            {
                PawnController tank = _tankBuffer[i];
                if (tank == null || tank.currentHP <= 0) continue;

                if (FXManager.Instance != null) FXManager.Instance.SetUnitFocus(tank.transform);

                // La carte d'unite nomme qui agit. Sans elle, la phase automatique
                // n'est qu'une suite de mouvements que le joueur subit : c'est ici
                // que la posture qu'il a choisie se montre en train d'operer.
                UnitActionCard.Focus(tank, i + 1);
                yield return _waitFocus;

                // --- Choix de cible pondere, filtre par la posture. Voir SelectTankTarget.
                PawnController targetPawn;
                Hexagon targetHex;
                SelectTankTarget(tank, out targetPawn, out targetHex);

                if (targetPawn == null && targetHex == null) continue;

                HexCoord targetCoord = (targetPawn != null) ? targetPawn.hexcoord : targetHex.positionInTheBoard;
                int distance = BoardController.GetHexDistance(tank.hexcoord, targetCoord);

                if (distance <= tank.attackRange)
                {
                    // Attaque : orientation puis resolution deterministe.
                    UnitActionCard.Attack(tank, targetPawn, targetHex);

                    // On cadre le MILIEU du duel, un peu plus large : voir partir un
                    // tir dont la cible est hors champ n'apprend rien au joueur.
                    Vector3 impact = (targetPawn != null)
                                     ? targetPawn.transform.position
                                     : targetHex.transform.position;
                    CameraDirector.FrameAction(tank.transform.position, impact);
                    tank.attackCoord = targetCoord;
                    tank.ApplySequenceAnimation(PawnController.TypeOfPawnInteractions.attack);
                    yield return _waitAim;

                    if (targetPawn != null) InteractionRules.ResolveCombat(tank, targetPawn);
                    else InteractionRules.ResolveCombat(tank, targetHex);

                    yield return _waitResolve;
                }
                else
                {
                    // Un Tank Niveau 2 avance de deux cases : la boucle rejoue le pas
                    // autant de fois que sa mobilite le permet, en s'arretant des
                    // qu'il entre a portee de tir.
                    UnitActionCard.Move(tank);

                    int steps = tank.moveRange > 0 ? tank.moveRange : 1;

                    // Sous le commandement d'un Centre : une case de plus. C'est ce qui
                    // ramene un Shofar a sept cases dans le temps d'une fenetre de
                    // bouclier au lieu du double.
                    if (BuildingManager.Instance != null
                        && BuildingManager.Instance.HasCommandSupport(tank.hexcoord))
                        steps += InteractionRules.MOUNTAIN_MOVE_BONUS;

                    for (int s = 0; s < steps; s++)
                    {
                        if (BoardController.GetHexDistance(tank.hexcoord, targetCoord) <= tank.attackRange) break;

                        HexCoord nextCoord = board.GetNextStepTowards(tank.hexcoord, targetCoord, false);
                        if (nextCoord == null || nextCoord.CompareHexCoord(tank.hexcoord)) break;

                        tank.targetCoord = nextCoord;
                        tank.ApplySequenceAnimation(PawnController.TypeOfPawnInteractions.target);
                        yield return _waitMove;
                    }
                }

                yield return _waitStep;
            }

            if (FXManager.Instance != null) FXManager.Instance.SetUnitFocus(null);
            UnitActionCard.Dismiss();
        }

        /// <summary>
        /// Choix de cible d'un Tank, module par sa posture.
        ///
        ///   Assaut : les Portails pesent lourd, les ennemis presque rien. Le Tank
        ///            traverse la carte et ne se laisse pas distraire.
        ///   Garde  : il ne considere que ce qui se trouve a guardRadius de la Base,
        ///            et ignore totalement les Portails. C'est le mur.
        ///   Chasse : seuls les ennemis comptent, et la distance domine tout.
        ///
        /// Tous les poids restent exposes dans l'inspecteur pour l'equilibrage.
        /// </summary>
        private void SelectTankTarget(PawnController tank, out PawnController targetPawn, out Hexagon targetHex)
        {
            targetPawn = null;
            targetHex = null;

            BoardController board = BoardController.instance;
            if (board == null || tank == null || tank.hexcoord == null) return;

            PawnStance stance = tank.stance;

            int bestScore = int.MinValue;
            int damage = InteractionRules.GetPawnDamage(tank);
            BuildingManager buildings = BuildingManager.Instance;
            PortalManager portalState = PortalManager.Instance;

            // Multiplicateurs de posture, en pourcentage pour rester en entiers.
            int portalFactor;  // attrait des Portails
            int enemyFactor;   // attrait des ennemis
            int distanceFactor;

            switch (stance)
            {
                case PawnStance.Assault: portalFactor = 140; enemyFactor = 35; distanceFactor = 70; break;
                case PawnStance.Hunt: portalFactor = 0; enemyFactor = 140; distanceFactor = 180; break;
                default: portalFactor = 0; enemyFactor = 120; distanceFactor = 100; break; // Garde
            }

            // --- Portails : l'objectif de la partie (ignores en Garde et en Chasse) ---
            if (portalFactor > 0)
            {
                List<Hexagon> hexes = board.HexagonsInBoard;
                for (int i = 0; i < hexes.Count; i++)
                {
                    Hexagon hex = hexes[i];
                    if (hex == null || hex.type != TypeOfHex.portal) continue;

                    int distance = BoardController.GetHexDistance(tank.hexcoord, hex.positionInTheBoard);

                    int score = (portalPriority * portalFactor) / 100;
                    score -= (distance * distanceWeight * distanceFactor) / 100;

                    if (hex.level >= 2) score += portalLevel2Bonus;
                    if (distance <= tank.attackRange) score += inRangeBonus;
                    if (hex.currentHP > 0 && hex.currentHP <= damage) score += finishBonus;

                    // Bouclier tombe : c'est maintenant qu'il faut frapper.
                    if (portalState != null && portalState.IsShieldDown(hex)) score += portalShieldDownBonus;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        targetHex = hex;
                        targetPawn = null;
                    }
                }
            }

            // --- Ennemis ---
            List<PawnController> pawns = board.PawnsInBoard;
            for (int i = 0; i < pawns.Count; i++)
            {
                PawnController enemy = pawns[i];
                if (enemy == null || !enemy.IsEnemy || enemy.hexcoord == null || enemy.currentHP <= 0) continue;

                // Distance au point d'ancrage le plus proche : la Base, OU un Centre de
                // Commandement debout. C'est ce qui permet de tenir du terrain ailleurs
                // que chez soi - avec la seule Base, un garde poste en avant ignorait
                // l'ennemi plante devant lui.
                int distanceToBase = (buildings != null)
                                     ? buildings.DistanceToCommandPoint(enemy.hexcoord)
                                     : int.MaxValue;

                // En Garde, le Tank refuse de courir apres une cible hors de son perimetre.
                if (stance == PawnStance.Guard
                    && distanceToBase != int.MaxValue
                    && distanceToBase > guardRadius) continue;

                int distance = BoardController.GetHexDistance(tank.hexcoord, enemy.hexcoord);

                int score = (enemyPriority * enemyFactor) / 100;
                score -= (distance * distanceWeight * distanceFactor) / 100;

                // Menace : chaque case gagnee vers la Base sous le rayon d'alerte compte.
                if (distanceToBase != int.MaxValue && distanceToBase < threatRadius)
                {
                    int threat = (threatRadius - distanceToBase) * threatWeight;
                    if (stance == PawnStance.Assault) threat /= 3;   // l'assaut ne se laisse pas rappeler
                    score += threat;
                }

                if (enemy.attackRange >= 2) score += rangedEnemyBonus;
                if (distance <= tank.attackRange) score += inRangeBonus;
                if (enemy.currentHP <= damage) score += finishBonus;

                // Legitime defense. Un Tank en Assaut campe devant un Portail : sans
                // ce bonus, il tapait la structure pendant que les gardiens le
                // criblaient dans le dos, et l'offensive etait mathematiquement
                // intenable. Il abat d'abord ce qui est deja a portee - sauf s'il
                // peut fermer le Portail ce tour-ci, auquel cas finishBonus l'emporte.
                if (stance == PawnStance.Assault && distance <= tank.attackRange)
                    score += assaultSelfDefenseBonus;

                if (score > bestScore)
                {
                    bestScore = score;
                    targetPawn = enemy;
                    targetHex = board.getHexByCoord(enemy.hexcoord);
                }
            }

            // Garde sans cible : le Tank rentre se poster pres de la Base.
            if (targetPawn == null && targetHex == null && stance == PawnStance.Guard && buildings != null)
            {
                if (buildings.DistanceToBase(tank.hexcoord) > guardRadius)
                    targetHex = FindNearestBaseHex(board, tank.hexcoord);
            }
        }

        /// <summary>Hexagone de Base le plus proche, pour le repli des Tanks en Garde.</summary>
        private Hexagon FindNearestBaseHex(BoardController board, HexCoord from)
        {
            List<Hexagon> hexes = board.HexagonsInBoard;
            Hexagon best = null;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex == null || hex.type != TypeOfHex.Base) continue;

                int d = BoardController.GetHexDistance(from, hex.positionInTheBoard);
                if (d < bestDistance) { bestDistance = d; best = hex; }
            }
            return best;
        }

        // =================================================================
        //  5. FIN DE TOUR (appele par EnemyAI quand l'IA a fini)
        // =================================================================
        public void StartNextTurn()
        {
            if (gameOver) return;
            StartCoroutine(EndOfTurnPhase());
        }

        private IEnumerator EndOfTurnPhase()
        {
            phase = TurnPhase.EndOfTurn;

            // Effets des batiments : bunkers, gaz, cristaux, montagnes.
            if (BuildingManager.Instance != null)
                yield return StartCoroutine(BuildingManager.Instance.ProcessEndOfTurn());

            // Deploiement, instabilite et annonce de vague.
            if (PortalManager.Instance != null)
                PortalManager.Instance.ProcessPortals();

            // Filet de securite : un ennemi arrive par un autre chemin que le
            // deploiement (chargement de partie, script de test) n'aurait pas de
            // cordon. Un recensement par tour coute un parcours de liste.
            if (PortalTether.Instance != null) PortalTether.Instance.Refresh();

            if (BoardController.instance != null)
            {
                BoardController.instance.RefreshAllAuras();
                BoardController.instance.RefreshStructureHealthBars();
            }

            SerializeGameState();

            if (CheckEndOfGame()) yield break;

            currentTurn++;
            StartTurn();
        }

        /// <summary>Victoire : plus aucun portail. Defaite : la Base est tombee.</summary>
        public bool CheckEndOfGame()
        {
            if (BuildingManager.Instance != null && BuildingManager.Instance.IsBaseDestroyed())
            {
                EndGame(StateOfGame.defeat);
                return true;
            }

            if (PortalManager.Instance != null && PortalManager.Instance.AllPortalsDestroyed())
            {
                EndGame(StateOfGame.victory);
                return true;
            }

            return false;
        }

        private void EndGame(StateOfGame state)
        {
            gameOver = true;
            isPlayerTurn = false;
            phase = TurnPhase.Idle;

            Debug.Log("[TurnManager] Fin de partie : " + state);

            if (GameManager.instance != null)
            {
                GameManager.instance.canClickOrHover = false;
                GameManager.instance.m_stateOfGame = state;
            }
            if (GameManager.OnStateOfGameChanged != null) GameManager.OnStateOfGameChanged.Invoke(state);
            if (BoardController.instance != null) BoardController.instance.UpdateStateOfGame(state);

            // L'ecran de fin. Appel direct et non via l'evenement : OnStateOfGameChanged
            // etait l'affaire de l'ancien GameCanvas, qui est en cours de retrait. Le
            // seul moment de la partie qui doit conclure ne peut pas dependre d'un objet
            // qu'on s'apprete a supprimer.
            GameOverPanel.Report(state);
        }

        // =================================================================
        //  Debug / sauvegarde
        // =================================================================
        /// <summary>
        /// Instantane complet du plateau. Il alloue une liste de 169 HexagonData plus la
        /// chaine JSON : desactive par defaut, il ne tourne que si logStateEachTurn est coche.
        /// </summary>
        public void SerializeGameState()
        {
            if (!logStateEachTurn) return;
            if (BoardController.instance == null) return;

            List<HexagonData> currentBoardData = new List<HexagonData>();
            if (BoardController.instance.HexagonsInBoard != null)
            {
                foreach (Hexagon hex in BoardController.instance.HexagonsInBoard)
                {
                    if (hex == null) continue;
                    currentBoardData.Add(new HexagonData
                    {
                        typeID = hex.type,
                        type = hex.type.ToString(),
                        level = hex.level,
                        energy = hex.energy,
                        CP = hex.commandPoints,
                        to = hex.positionInTheBoard,
                        from = hex.positionInTheBoard
                    });
                }
            }

            int pawnsCount = BoardController.instance.PawnsInBoard != null ? BoardController.instance.PawnsInBoard.Count : 0;
            int energy = EnergyManager.Instance != null ? EnergyManager.Instance.CurrentEnergy : 0;
            int portals = PortalManager.Instance != null ? PortalManager.Instance.CountPortals() : 0;

            string json = JsonConvert.SerializeObject(new
            {
                turn = currentTurn,
                playerEnergy = energy,
                portalsLeft = portals,
                pawns_count = pawnsCount,
                data = currentBoardData
            });

            Debug.Log(json);
        }
    }
}

// ---------------------------------------------------------------------------
// NOTE D'OPTIMISATION
//
// 1. Update() ne fait plus tourner de minuterie par defaut (spendingDuration = 0) :
//    il se reduit a deux tests d'etat et deux Input.GetKeyDown, sans allocation.
// 2. Les multiplicateurs de posture sont appliques en entiers (pourcentages), pas en
//    float : le score reste un int et le tri ne fait aucune conversion.
// 3. Les WaitForSeconds sont des instances statiques partagees ; chaque "new" dans
//    une coroutine serait un dechet par unite et par tour.
// 4. _tankBuffer est un champ reutilise. FindAll(lambda) allouait une liste et une
//    closure a chaque phase.
// 5. SelectTankTarget parcourt les listes du plateau par index, sans LINQ ni closure,
//    et sort immediatement des Portails quand la posture les ignore.
// 6. phaseText.SetText("... {0} ...", value) ecrit dans le buffer interne de TMP :
//    aucune string intermediaire n'est creee.
// ---------------------------------------------------------------------------
