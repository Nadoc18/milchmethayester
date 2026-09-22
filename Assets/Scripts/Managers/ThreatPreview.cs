using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MNLTHII;
using MNLTHII.Rules;

namespace MNLTHII.Managers
{
    /// <summary>
    /// "Vision du Yetzer Hara" : des fantomes translucides rejouent en boucle ce que
    /// l'adversaire s'apprete a faire.
    ///
    /// Chaque intention devient une petite scene qui tourne en boucle :
    ///   apparition (FX de deploiement)  ->  deplacement ou attaque  ->  pause  ->  on recommence.
    ///
    /// L'attaque est montree par une explosion SANS secousse de camera : une secousse
    /// dirait "ca arrive maintenant", alors que justement ce n'est pas encore arrive.
    /// C'est la meme raison qui rend les modeles translucides.
    ///
    /// Pourquoi ce n'est pas de la triche : le combat est deterministe et l'IA choisit
    /// sa cible par un score. L'information existait deja, elle etait juste invisible.
    /// Le calcul reutilise EnemyAI.SelectEnemyTarget et PortalManager, sans les
    /// dupliquer : l'apercu ne peut donc pas mentir sur les regles.
    ///
    /// Ce qu'il montre, pour l'etat ACTUEL du plateau :
    ///   - un ennemi qui va se deplacer   -> un fantome glisse vers la case visee
    ///   - un ennemi qui va attaquer      -> un fantome se tourne et l'explosion part sur la cible
    ///   - un Portail qui va deployer     -> un fantome apparait sur la case de sortie
    ///
    /// C'est une PROJECTION, pas une certitude : les Tanks jouent avant les ennemis, et
    /// chaque action du joueur recalcule l'apercu.
    /// </summary>
    public class ThreatPreview : MonoBehaviour
    {
        public static ThreatPreview Instance { get; private set; }

        [Header("Commande")]
        [Tooltip("Touche qui affiche ou masque l'apercu.")]
        public KeyCode toggleKey = KeyCode.Tab;
        [Tooltip("Coche : l'apercu n'est visible que tant que la touche est maintenue.")]
        public bool holdToShow = false;

        [Header("Modeles fantomes")]
        [Tooltip("Optionnel. Index 0 = ennemi Niveau 1, 1 = Niveau 2, 2 = Niveau 3. " +
                 "Laisse vide : le fantome est clone sur l'ennemi reel present sur la carte.")]
        public GameObject[] ghostEnemyPrefabs = new GameObject[3];

        [Tooltip("Optionnel. Laisse vide : un materiau translucide est cree au demarrage.")]
        public Material ghostMaterial;

        [Header("Apparence")]
        [Range(0.05f, 1f)]
        [Tooltip("Transparence des fantomes. C'est ce qui dit : ce n'est pas encore arrive.")]
        public float ghostAlpha = 0.45f;
        [Tooltip("Echelle du fantome par rapport au modele reel.")]
        public float ghostScale = 1f;

        public Color moveColor = new Color(1f, 0.68f, 0.25f);
        public Color attackColor = new Color(1f, 0.3f, 0.25f);
        public Color spawnColor = new Color(0.78f, 0.45f, 1f);

        /// <summary>
        /// Ecartement des fantomes d'une vague autour de leur case de sortie.
        ///
        /// A 0.22 ils se chevauchaient : on devinait un groupe sans pouvoir le compter,
        /// alors que compter est justement le seul interet de les montrer tous.
        /// </summary>
        public float spawnSpread = 0.45f;

        [Header("Rythme de la boucle")]
        [Tooltip("Duree du glissement d'un fantome qui se deplace.")]
        public float moveDuration = 0.9f;
        [Tooltip("Temps d'attente avant que l'explosion d'attaque ne parte.")]
        public float aimDelay = 0.35f;
        [Tooltip("Temps pendant lequel le fantome reste en place avant de disparaitre.")]
        public float holdDuration = 2f;
        [Tooltip("Temps d'ecran vide avant que la boucle ne reprenne.")]
        public float gapDuration = 0.4f;

        [Header("Son")]
        [Tooltip("Decoche par defaut : la boucle rejouerait le son toutes les trois secondes.")]
        public bool playSounds = false;

        [Header("Tour de camera")]
        [Tooltip("A l'ouverture de l'apercu, la camera passe une fois sur chaque menace, puis revient.")]
        public bool cameraTour = true;

        [Tooltip("Temps passe sur chaque menace pendant le tour de camera. Nouveau nom : "
               + "l'ancien (0.85 s) etait trop rapide pour voir ce que faisait chaque ennemi.")]
        public float tourHoldPerThreat = 2.2f;

        [Tooltip("Delai avant le depart du tour : les fantomes doivent etre en place.")]
        public float tourStartDelay = 0.25f;

        [Header("Etat (lecture seule)")]
        public bool isVisible = false;
        public int ghostsShown = 0;

        // =================================================================
        //  ETAT INTERNE
        // =================================================================
        private const int MaxGhosts = 32;

        /// <summary>Un fantome : son modele, ses renderers, et la boucle qui l'anime.</summary>
        private class Ghost
        {
            public Transform transform;
            public GameObject gameObject;
            public Renderer[] renderers;

            /// <summary>
            /// Echelle reelle du modele d'origine. Les pions vivent sous leur hexagone
            /// et heritent de son echelle : forcer localScale a 1 donnait des fantomes
            /// gigantesques. On memorise l'echelle a la construction et on ne fait plus
            /// que la multiplier par ghostScale.
            /// </summary>
            public Vector3 baseScale;

            public int level;          // niveau d'ennemi represente, pour reutiliser le bon modele
            public bool fromClone;     // construit en clonant une unite vivante
            public bool inUse;
            public Coroutine routine;
        }

        private readonly List<Ghost> _ghosts = new List<Ghost>(MaxGhosts);

        // Deux racines : une desactivee ou l'on fabrique les fantomes (un objet cree
        // sous un parent inactif ne declenche AUCUN Awake, donc aucun script clone ne
        // demarre), et une active ou ils sont joues une fois nettoyes.
        private Transform _stagingRoot;
        private Transform _liveRoot;

        private MaterialPropertyBlock _block;
        private int _colorPropertyId = 0;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        // Attentes mises en cache : un "new WaitForSeconds" par boucle et par fantome
        // serait un dechet regulier. Elles sont reconstruites quand les durees changent.
        private WaitForSeconds _waitAim;
        private WaitForSeconds _waitHold;
        private WaitForSeconds _waitGap;
        private float _cachedAim = -1f, _cachedHold = -1f, _cachedGap = -1f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(this); return; }

            _block = new MaterialPropertyBlock();

            // Les champs statiques survivent a une sortie du mode Play dans l'editeur :
            // on repart d'un cache vide a chaque demarrage.
            _boardPrefabsCache = null;
            _boardPrefabsResolved = false;

            // Les deux racines sont des objets de scene SANS parent : leur echelle vaut
            // donc exactement 1, et le localScale d'un fantome est son echelle monde.
            // Les accrocher a ce GameObject aurait suffi a fausser toutes les tailles
            // si celui-ci n'etait pas a l'echelle 1.
            GameObject staging = new GameObject("ThreatPreview_Staging");
            staging.SetActive(false);              // indispensable : aucun Awake sur les clones
            _stagingRoot = staging.transform;

            GameObject live = new GameObject("ThreatPreview_Ghosts");
            _liveRoot = live.transform;

            EnsureMaterial();
        }

        private void OnDestroy()
        {
            if (_stagingRoot != null) Destroy(_stagingRoot.gameObject);
            if (_liveRoot != null) Destroy(_liveRoot.gameObject);
        }

        // =================================================================
        //  COMMANDE
        // =================================================================
        private void Update()
        {
            if (holdToShow)
            {
                if (Input.GetKeyDown(toggleKey)) Show();
                else if (Input.GetKeyUp(toggleKey)) Hide();
                return;
            }

            if (Input.GetKeyDown(toggleKey)) TogglePreview();
        }

        /// <summary>
        /// A brancher sur le OnClick d'un bouton d'interface. C'est aussi ce
        /// qu'appelle la touche definie par toggleKey.
        /// </summary>
        public void TogglePreview()
        {
            if (isVisible) Hide();
            else Show();
        }

        public void Show()
        {
            isVisible = true;
            RefreshWaits();
            Rebuild();

            // Un tour de reconnaissance : la camera va voir chaque menace une fois,
            // puis revient. Sans lui, six fantomes s'animent simultanement aux quatre
            // coins du plateau et le joueur n'en regarde aucun vraiment - l'apercu
            // devient une agitation de fond au lieu d'une information.
            //
            // Le tour ne passe qu'une seule fois. Les fantomes, eux, continuent leur
            // boucle : une fois la carte en tete, on veut pouvoir relire n'importe
            // quel coin sans qu'une camera nous impose son itineraire.
            StartCameraTour();
        }

        public void Hide()
        {
            isVisible = false;
            StopCameraTour();
            ReleaseAll();

            CameraDirector.ReleaseCamera();
        }

        // =================================================================
        //  TOUR DE CAMERA
        // =================================================================
        private Coroutine _tour;

        private void StartCameraTour()
        {
            StopCameraTour();
            if (!cameraTour) return;
            if (CameraDirector.Instance == null) return;

            _tour = StartCoroutine(CameraTour());
        }

        private void StopCameraTour()
        {
            if (_tour == null) return;

            StopCoroutine(_tour);
            _tour = null;
        }

        /// <summary>
        /// Passe une fois sur chaque fantome actif, puis rend la camera.
        ///
        /// On relit la position a chaque etape plutot que de la capturer d'avance :
        /// un fantome qui se deplace a bouge entre le moment ou le tour a commence et
        /// celui ou son tour arrive, et cadrer l'endroit ou il ETAIT ne montrerait
        /// rien. Le tour s'interrompt de lui-meme si l'apercu est referme entre-temps.
        /// </summary>
        private IEnumerator CameraTour()
        {
            yield return new WaitForSecondsRealtime(tourStartDelay);

            for (int i = 0; i < _ghosts.Count; i++)
            {
                if (!isVisible) break;

                Ghost ghost = _ghosts[i];
                if (ghost == null || !ghost.inUse || ghost.transform == null) continue;

                CameraDirector.FocusPoint(ghost.transform.position);

                yield return new WaitForSecondsRealtime(tourHoldPerThreat);
            }

            CameraDirector.ReleaseCamera();
            _tour = null;
        }

        /// <summary>
        /// Recalcule l'apercu s'il est affiche. Appele apres chaque action du joueur :
        /// poser un Bunker ou creer un Tank change ce que l'ennemi va faire, et
        /// l'apercu doit le refleter immediatement.
        /// </summary>
        public void RefreshIfVisible()
        {
            if (isVisible) Rebuild();
        }

        private void RefreshWaits()
        {
            if (!Mathf.Approximately(_cachedAim, aimDelay)) { _cachedAim = aimDelay; _waitAim = new WaitForSeconds(aimDelay); }
            if (!Mathf.Approximately(_cachedHold, holdDuration)) { _cachedHold = holdDuration; _waitHold = new WaitForSeconds(holdDuration); }
            if (!Mathf.Approximately(_cachedGap, gapDuration)) { _cachedGap = gapDuration; _waitGap = new WaitForSeconds(gapDuration); }
        }

        // =================================================================
        //  CALCUL DE L'APERCU
        // =================================================================
        /// <summary>
        /// Rang du prochain fantome dans la sequence. C'est lui qui decale les
        /// apparitions les unes apres les autres, au lieu de les faire jaillir toutes
        /// ensemble.
        /// </summary>
        private int _sequenceIndex;

        private void Rebuild()
        {
            RefreshWaits();
            ReleaseAll();

            _sequenceIndex = 0;

            BoardController board = BoardController.instance;
            if (board == null || board.PawnsInBoard == null) return;

            PreviewEnemies(board);
            PreviewPortals(board);

            ghostsShown = CountInUse();

            // Trace de controle : si ce compte est a zero, il n'y a tout simplement
            // aucune intention a montrer (aucun ennemi sur la carte, aucun portail
            // pret a deployer) - le probleme n'est pas dans l'affichage.
            Debug.LogFormat("[ThreatPreview] {0} intention(s) affichee(s).", ghostsShown);
        }

        /// <summary>
        /// Pour chaque ennemi : la meme fonction de score que celle qui sera reellement
        /// jouee, puis le meme calcul de pas que celui de la phase ennemie.
        /// </summary>
        private void PreviewEnemies(BoardController board)
        {
            EnemyAI ai = EnemyAI.Instance;
            if (ai == null) return;

            List<PawnController> pawns = board.PawnsInBoard;

            for (int i = 0; i < pawns.Count; i++)
            {
                PawnController enemy = pawns[i];
                if (enemy == null || !enemy.IsEnemy || enemy.currentHP <= 0 || enemy.hexcoord == null) continue;

                PawnController targetPawn;
                Hexagon targetHex;
                ai.SelectEnemyTarget(enemy, out targetPawn, out targetHex);

                if (targetPawn == null && targetHex == null) continue;

                HexCoord targetCoord = (targetPawn != null) ? targetPawn.hexcoord : targetHex.positionInTheBoard;
                if (targetCoord == null) continue;

                int distance = BoardController.GetHexDistance(enemy.hexcoord, targetCoord);
                Vector3 from = enemy.transform.position;

                if (distance <= enemy.attackRange)
                {
                    // Attaque : le fantome reste sur place, se tourne, et l'explosion
                    // part sur la victime.
                    Vector3 victim = (targetPawn != null) ? targetPawn.transform.position : targetHex.transform.position;
                    SpawnGhost(enemy.level, enemy, from, victim, true, attackColor);
                }
                else
                {
                    // Deplacement : le fantome glisse vers la case ou il arrivera.
                    HexCoord next = board.GetNextStepTowards(enemy.hexcoord, targetCoord, true);
                    if (next == null || next.CompareHexCoord(enemy.hexcoord)) continue;

                    Hexagon destination = board.getHexByCoord(next);
                    if (destination == null) continue;

                    SpawnGhost(enemy.level, enemy, from, destination.transform.position, false, moveColor);
                }
            }
        }

        /// <summary>
        /// Portails : combien d'ennemis vont sortir en fin de tour, et ou. C'est
        /// l'information qui permet de decider ou investir AVANT que la vague ne tombe.
        /// </summary>
        private void PreviewPortals(BoardController board)
        {
            PortalManager portals = PortalManager.Instance;
            if (portals == null || board.HexagonsInBoard == null) return;

            List<Hexagon> hexes = board.HexagonsInBoard;

            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon portal = hexes[i];
                if (portal == null || portal.type != TypeOfHex.portal) continue;

                int count = portals.PredictSpawnCount(portal);
                if (count <= 0) continue;

                Hexagon exit = portals.PredictSpawnHex(portal);
                if (exit == null) continue;

                bool surging = portals.IsSurgeAnnounced(portal);
                Vector3 center = exit.transform.position;

                // Une vague annoncee sort plusieurs ennemis : autant de fantomes,
                // disposes en cercle autour de la case de sortie pour qu'on les compte
                // d'un coup d'oeil.
                for (int k = 0; k < count; k++)
                {
                    Vector3 spot = center;

                    if (count > 1)
                    {
                        float angle = (360f / count) * k * Mathf.Deg2Rad;
                        spot += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spawnSpread;
                    }

                    // Le niveau n'est plus devine : PortalManager l'a tire a l'avance et
                    // le deploiement consommera CE niveau-la. Le fantome porte donc le
                    // modele exact de l'ennemi qui va sortir - un Niveau 3 se reconnait
                    // a sa silhouette avant d'etre la.
                    int previewLevel = portals.PredictSpawnLevel(portal, k);
                    if (previewLevel < 1) previewLevel = 1;

                    SpawnGhost(previewLevel, null, spot, spot, false, surging ? attackColor : spawnColor);
                }
            }
        }

        // =================================================================
        //  FANTOMES
        // =================================================================
        private void SpawnGhost(int level, PawnController liveModel, Vector3 from, Vector3 to, bool isAttack, Color color)
        {
            Ghost ghost = AcquireGhost(level, liveModel);

            if (ghost == null)
            {
                // Aucun modele disponible : on garde au moins le signal lumineux.
                if (FXManager.Instance != null) FXManager.Instance.SpawnEnemyFX(to);
                return;
            }

            color.a = ghostAlpha;
            Tint(ghost, color);

            ghost.transform.position = from;

            // Echelle du modele d'origine, simplement multipliee par le reglage.
            // _liveRoot est un objet racine a l'echelle 1, donc localScale = echelle monde.
            ghost.transform.localScale = ghost.baseScale * ghostScale;

            Vector3 facing = to - from;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f) ghost.transform.rotation = Quaternion.LookRotation(facing);

            SetRenderersEnabled(ghost, false);
            ghost.gameObject.SetActive(true);

            // Chaque menace attend son tour. Le fantome reste invisible pendant son
            // delai - les renderers viennent d'etre eteints, et GhostLoop ne les
            // rallume qu'a l'apparition.
            float delay = SequenceDelay(_sequenceIndex);
            _sequenceIndex++;

            ghost.routine = StartCoroutine(GhostLoop(ghost, from, to, isAttack, delay));
        }

        /// <summary>
        /// Quand le fantome de rang index doit apparaitre.
        ///
        /// Les six menaces jaillissaient toutes en meme temps : la camera passait de
        /// l'une a l'autre pendant que les six rejouaient leur boucle en choeur, et
        /// on ne voyait que de l'agitation. Ici chaque apparition attend son rang, sur
        /// EXACTEMENT la meme cadence que le tour de camera - donc la menace se montre
        /// au moment ou le regard arrive dessus.
        ///
        /// On ajoute la duree de deplacement de la camera : elle met ce temps a
        /// atteindre sa cible, et apparaitre avant qu'elle soit arrivee reviendrait a
        /// jouer la scene hors champ.
        ///
        /// Le decalage survit au tour : les boucles restent dephasees ensuite, ce qui
        /// vaut mieux de toute facon qu'un plateau qui clignote a l'unisson.
        /// </summary>
        private float SequenceDelay(int index)
        {
            float delay = index * tourHoldPerThreat;

            if (cameraTour && CameraDirector.Instance != null)
                delay += tourStartDelay + CameraDirector.Instance.moveDuration;

            return delay;
        }

        /// <summary>
        /// La boucle : apparition, action, pause, on recommence. Elle ne s'arrete
        /// jamais d'elle-meme ; c'est Hide ou Rebuild qui la coupe.
        /// </summary>
        private IEnumerator GhostLoop(Ghost ghost, Vector3 from, Vector3 to, bool isAttack,
                                      float startDelay)
        {
            FXManager fx = FXManager.Instance;
            bool moves = !isAttack && (to - from).sqrMagnitude > 0.0001f;

            // Attente de son rang, comptee a la main plutot qu'avec un
            // WaitForSeconds : la duree change d'un fantome a l'autre, donc l'instance
            // ne serait pas reutilisable et on en allouerait une par menace a chaque
            // ouverture de l'apercu.
            if (startDelay > 0f)
            {
                // Temps NON mis a l'echelle, comme le tour de camera : si les deux
                // comptaient differemment, ils se decaleraient des que Time.timeScale
                // bouge, et c'est precisement leur synchronisation qu'on cherche.
                float waited = 0f;
                while (waited < startDelay)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            while (true)
            {
                // --- Apparition ---
                ghost.transform.position = from;
                SetRenderersEnabled(ghost, true);

                if (fx != null) fx.SpawnEnemyFX(from);
                if (playSounds && fx != null) fx.PlayEnemySpawnerSFX();

                // --- Action ---
                if (moves)
                {
                    float elapsed = 0f;
                    float duration = (moveDuration > 0.05f) ? moveDuration : 0.05f;

                    while (elapsed < duration)
                    {
                        elapsed += Time.deltaTime;
                        float t = Mathf.Clamp01(elapsed / duration);
                        // Adoucissement : depart et arrivee moins secs qu'un lerp brut.
                        t = t * t * (3f - 2f * t);
                        ghost.transform.position = Vector3.Lerp(from, to, t);
                        yield return null;
                    }

                    ghost.transform.position = to;
                }
                else if (isAttack)
                {
                    yield return _waitAim;

                    // Explosion sur la cible. SpawnDestructionFX ne touche PAS a la
                    // camera : une secousse ferait croire que le coup est reellement
                    // porte, alors que c'est une prevision.
                    if (fx != null) fx.SpawnDestructionFX(to + Vector3.up * 0.5f);
                    if (playSounds && fx != null) fx.PlayExplosionSFX();
                }

                // --- Pause, puis on recommence ---
                yield return _waitHold;

                SetRenderersEnabled(ghost, false);
                yield return _waitGap;
            }
        }

        // =================================================================
        //  APERCU D'UN CONSEIL (cote joueur)
        // =================================================================
        //
        // Le conseiller propose deux ou trois actions ; au survol d'une carte, le
        // joueur doit VOIR ce qu'elle ferait sur le plateau. Un texte ne suffit pas :
        // "envoie ce Tank sur le Shofar de l'est" ne dit pas lequel, ni d'ou, ni
        // combien de cases. Un Tank translucide qui parcourt le trajet le dit d'un
        // seul regard.
        //
        // Cet apercu est INDEPENDANT de l'apercu de menace : il a son propre fantome
        // et sa propre coroutine, donc ouvrir l'un n'eteint pas l'autre et le joueur
        // peut comparer sa reponse a la menace qu'il vient de regarder.
        private Ghost _hint;
        private Coroutine _hintRoutine;

        /// <summary>
        /// Montre ce qu'une action ferait : un Tank translucide part de "from" et va
        /// vers "to". isAttack le fait frapper sur place au lieu d'avancer.
        /// </summary>
        public void ShowHint(Vector3 from, Vector3 to, bool isAttack, Color color)
        {
            HideHint();

            // Niveau -1 : la cle de pool des fantomes du joueur.
            Ghost ghost = AcquireGhost(-1, null);
            if (ghost == null) return;

            _hint = ghost;

            color.a = ghostAlpha;
            Tint(ghost, color);

            ghost.transform.position = from;
            ghost.transform.localScale = ghost.baseScale * ghostScale;

            Vector3 facing = to - from;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f) ghost.transform.rotation = Quaternion.LookRotation(facing);

            SetRenderersEnabled(ghost, true);
            ghost.gameObject.SetActive(true);

            RefreshWaits();
            _hintRoutine = StartCoroutine(HintLoop(ghost, from, to, isAttack));
        }

        public void HideHint()
        {
            if (_hintRoutine != null)
            {
                StopCoroutine(_hintRoutine);
                _hintRoutine = null;
            }

            if (_hint == null) return;

            _hint.inUse = false;
            _hint.routine = null;
            if (_hint.gameObject != null) _hint.gameObject.SetActive(false);
            _hint = null;
        }

        /// <summary>
        /// Boucle d'apercu. Volontairement plus sobre que celle des menaces : pas de
        /// FX d'apparition ennemi, pas de son. C'est une proposition, pas un evenement.
        /// </summary>
        private IEnumerator HintLoop(Ghost ghost, Vector3 from, Vector3 to, bool isAttack)
        {
            bool moves = !isAttack && (to - from).sqrMagnitude > 0.0001f;

            while (true)
            {
                ghost.transform.position = from;
                SetRenderersEnabled(ghost, true);

                if (moves)
                {
                    float elapsed = 0f;
                    float duration = (moveDuration > 0.05f) ? moveDuration : 0.05f;

                    while (elapsed < duration)
                    {
                        elapsed += Time.deltaTime;
                        float t = Mathf.Clamp01(elapsed / duration);
                        t = t * t * (3f - 2f * t);
                        ghost.transform.position = Vector3.Lerp(from, to, t);
                        yield return null;
                    }

                    ghost.transform.position = to;
                }
                else if (isAttack)
                {
                    yield return _waitAim;

                    if (FXManager.Instance != null)
                        FXManager.Instance.SpawnAttackFX(to + Vector3.up * 0.5f);
                }

                yield return _waitHold;

                SetRenderersEnabled(ghost, false);
                yield return _waitGap;
            }
        }

        // =================================================================
        //  POOL DE FANTOMES
        // =================================================================
        /// <summary>
        /// Recupere un fantome du bon niveau, en le fabriquant la premiere fois.
        /// Les fantomes sont mis en pool par niveau : passer l'apercu en boucle ne
        /// declenche donc ni Instantiate ni Destroy une fois le premier tour joue.
        /// </summary>
        private Ghost AcquireGhost(int level, PawnController liveModel)
        {
            // Un niveau NEGATIF designe un fantome du joueur, pose par le conseiller.
            // Il a ainsi sa propre entree dans le pool, sans rien changer aux fantomes
            // d'ennemis qui gardent exactement les memes cles qu'avant.
            if (level == 0) level = 1;

            for (int i = 0; i < _ghosts.Count; i++)
            {
                Ghost candidate = _ghosts[i];
                if (!candidate.inUse && candidate.level == level && candidate.transform != null)
                {
                    candidate.inUse = true;
                    return candidate;
                }
            }

            if (CountInUse() >= MaxGhosts) return null;

            Ghost created = BuildGhost(level, liveModel);
            if (created == null) return null;

            created.inUse = true;
            _ghosts.Add(created);
            return created;
        }

        private Ghost BuildGhost(int level, PawnController liveModel)
        {
            GameObject source = null;
            bool fromClone = false;

            int index = Mathf.Clamp(level - 1, 0, 2);

            // 0. Fantome du JOUEUR : le conseiller montre ce qu'une action ferait. On
            //    prend le modele de Tank du plateau, ou a defaut un Tank vivant.
            if (level < 0)
            {
                source = GetPlayerPrefabFromBoard();

                if (source == null)
                {
                    PawnController anyTank = FindAnyPlayerTank();
                    if (anyTank != null) { source = anyTank.gameObject; fromClone = true; }
                }
            }
            // 1. Un prefab dedie, s'il a ete assigne dans l'inspecteur.
            else if (ghostEnemyPrefabs != null && index < ghostEnemyPrefabs.Length && ghostEnemyPrefabs[index] != null)
            {
                source = ghostEnemyPrefabs[index];
            }
            // 2. Sinon, on clone l'ennemi vivant : le fantome a forcement le bon modele.
            else if (liveModel != null)
            {
                source = liveModel.gameObject;
                fromClone = true;
            }
            else
            {
                // 3. Le prefab d'ennemi du plateau. Indispensable pour annoncer un
                //    deploiement AVANT qu'aucun ennemi n'existe : aux premiers tours la
                //    carte est vide, et sans cela il ne restait que le FX d'apparition.
                source = GetEnemyPrefabFromBoard(level);

                // 4. En dernier recours, n'importe quel ennemi deja sur la carte.
                if (source == null)
                {
                    PawnController any = FindAnyEnemy();
                    if (any != null) { source = any.gameObject; fromClone = true; }
                }
            }

            if (source == null)
            {
                Debug.LogWarningFormat("[ThreatPreview] Aucun modele disponible pour un fantome Niveau {0}. " +
                                       "Assigne ghostEnemyPrefabs dans l'inspecteur pour y remedier.", level);
                return null;
            }

            // L'echelle MONDE a laquelle le fantome doit apparaitre. Un pion vit sous
            // son hexagone et herite de son echelle : pour un clone, lossyScale donne
            // deja la bonne valeur ; pour un prefab, il faut la reconstituer.
            Vector3 worldScale = fromClone
                ? source.transform.lossyScale
                : Vector3.Scale(source.transform.localScale, GetBoardHexScale());

            if (worldScale.sqrMagnitude < 0.000001f) worldScale = Vector3.one;

            // Instancie SOUS LA RACINE DESACTIVEE : aucun Awake ne se declenche, donc
            // aucun script clone (PawnController, barre de vie, audio) ne demarre.
            GameObject instance = Instantiate(source, _stagingRoot);
            instance.name = "Ghost_L" + level;

            // On le desactive AVANT de le reparenter : sans cela, le raccrocher a une
            // racine active le reveillerait et declencherait les Awake qu'on veut eviter.
            instance.SetActive(false);

            StripToVisuals(instance);

            instance.transform.SetParent(_liveRoot, false);

            Ghost ghost = new Ghost();
            ghost.gameObject = instance;
            ghost.transform = instance.transform;
            ghost.renderers = instance.GetComponentsInChildren<Renderer>(true);
            ghost.baseScale = worldScale;
            ghost.level = level;
            ghost.fromClone = fromClone;

            int rendererCount = (ghost.renderers != null) ? ghost.renderers.Length : 0;

            if (rendererCount == 0)
            {
                // Le modele source n'a rien de visible, ou le nettoyage a ete trop loin.
                // On le dit franchement plutot que de laisser un fantome invisible.
                Debug.LogErrorFormat("[ThreatPreview] Le fantome Niveau {0} n'a aucun renderer : rien ne sera visible.", level);
            }
            else
            {
                Debug.LogFormat("[ThreatPreview] Fantome Niveau {0} construit ({1} renderer(s), echelle {2:0.###}).",
                                level, rendererCount, worldScale.x);
            }

            ApplyGhostMaterial(ghost);
            return ghost;
        }

        /// <summary>
        /// Echelle monde d'un hexagone du plateau. Les pions en sont enfants, donc un
        /// fantome construit depuis un prefab doit en tenir compte pour avoir la bonne
        /// taille a l'ecran.
        /// </summary>
        private static Vector3 GetBoardHexScale()
        {
            BoardController board = BoardController.instance;
            if (board == null || board.HexagonsInBoard == null) return Vector3.one;

            List<Hexagon> hexes = board.HexagonsInBoard;
            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex != null) return hex.transform.lossyScale;
            }
            return Vector3.one;
        }

        // Le tableau de prefabs du plateau est un champ prive serialise. On le lit une
        // seule fois par reflexion, et on garde le resultat : rien n'est modifie, et
        // BoardController n'a pas besoin d'etre touche pour cette fonctionnalite
        // d'affichage. Si le champ venait a changer de nom, on retombe simplement sur
        // les autres sources de modele au lieu de casser quoi que ce soit.
        private static GameObject[] _boardPrefabsCache;
        private static bool _boardPrefabsResolved;

        private static GameObject GetEnemyPrefabFromBoard(int level)
        {
            if (!_boardPrefabsResolved)
            {
                BoardController board = BoardController.instance;

                // Tant que le plateau n'existe pas, on ne fige rien : on reessaiera.
                if (board != null)
                {
                    _boardPrefabsResolved = true;

                    System.Reflection.FieldInfo field = typeof(BoardController).GetField(
                        "m_HexagonPrefabs",
                        System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Public);

                    if (field != null) _boardPrefabsCache = field.GetValue(board) as GameObject[];
                }
            }

            GameObject[] prefabs = _boardPrefabsCache;
            if (prefabs == null || prefabs.Length == 0) return null;

            int[] enemyIndices = PrefabsPath.GetPawnPrefabs(TypeOfPawn.enemy);
            if (enemyIndices == null || enemyIndices.Length == 0) return null;

            int slot = Mathf.Clamp(level - 1, 0, enemyIndices.Length - 1);
            int prefabIndex = enemyIndices[slot];

            if (prefabIndex < 0 || prefabIndex >= prefabs.Length) return null;
            return prefabs[prefabIndex];
        }

        /// <summary>
        /// Modele de Tank du joueur, lu dans le tableau de prefabs du plateau. Meme
        /// mecanique que pour les ennemis : rien n'est modifie dans BoardController.
        /// </summary>
        private static GameObject GetPlayerPrefabFromBoard()
        {
            GetEnemyPrefabFromBoard(1);          // force la resolution du tableau
            GameObject[] prefabs = _boardPrefabsCache;
            if (prefabs == null || prefabs.Length == 0) return null;

            int[] unitIndices = PrefabsPath.GetPawnPrefabs(TypeOfPawn.unit);
            if (unitIndices == null || unitIndices.Length == 0) return null;

            int prefabIndex = unitIndices[0];
            if (prefabIndex < 0 || prefabIndex >= prefabs.Length) return null;
            return prefabs[prefabIndex];
        }

        private PawnController FindAnyPlayerTank()
        {
            BoardController board = BoardController.instance;
            if (board == null || board.PawnsInBoard == null) return null;

            List<PawnController> pawns = board.PawnsInBoard;
            for (int i = 0; i < pawns.Count; i++)
            {
                PawnController pawn = pawns[i];
                if (pawn != null && !pawn.IsEnemy
                    && pawn.typeOfPawn == TypeOfPawn.unit && pawn.currentHP > 0) return pawn;
            }
            return null;
        }

        private PawnController FindAnyEnemy()
        {
            BoardController board = BoardController.instance;
            if (board == null || board.PawnsInBoard == null) return null;

            List<PawnController> pawns = board.PawnsInBoard;
            for (int i = 0; i < pawns.Count; i++)
            {
                PawnController pawn = pawns[i];
                if (pawn != null && pawn.IsEnemy && pawn.currentHP > 0) return pawn;
            }
            return null;
        }

        /// <summary>
        /// Ne garde que ce qui se voit. Tout le reste est retire : un fantome ne doit
        /// ni jouer de son, ni bloquer un clic, ni etre pris pour une unite reelle par
        /// le reste du jeu.
        /// </summary>
        private static void StripToVisuals(GameObject instance)
        {
            // D'abord ce que BoardReadability a ajoute a un ennemi vivant (ombre, anneau,
            // lisere) : un fantome clone n'en veut pas. A faire AVANT la boucle suivante,
            // qui detruit l'etiquette ReadabilityDecal qui permet de les reconnaitre.
            BoardReadability.StripDecorations(instance);

            // Plusieurs passes : un script marque [RequireComponent] refuse d'etre
            // retire tant que celui qui en depend est encore la. On reessaie, et ce
            // qui resiste est au moins desactive.
            for (int pass = 0; pass < 3; pass++)
            {
                MonoBehaviour[] scripts = instance.GetComponentsInChildren<MonoBehaviour>(true);
                if (scripts.Length == 0) break;

                for (int i = 0; i < scripts.Length; i++)
                {
                    MonoBehaviour script = scripts[i];
                    if (script == null) continue;

                    try { DestroyImmediate(script); }
                    catch (System.Exception) { script.enabled = false; }
                }
            }

            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i] != null) DestroyImmediate(colliders[i]);

            Rigidbody[] bodies = instance.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
                if (bodies[i] != null) DestroyImmediate(bodies[i]);

            AudioSource[] audios = instance.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < audios.Length; i++)
                if (audios[i] != null) DestroyImmediate(audios[i]);

            ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
                if (particles[i] != null) DestroyImmediate(particles[i].gameObject);

            Light[] lights = instance.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
                if (lights[i] != null) DestroyImmediate(lights[i]);

            Canvas[] canvases = instance.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
                if (canvases[i] != null) DestroyImmediate(canvases[i].gameObject);

            instance.layer = 2;   // Ignore Raycast : un fantome n'intercepte aucun clic.
        }

        private void ApplyGhostMaterial(Ghost ghost)
        {
            if (ghost.renderers == null || ghostMaterial == null) return;

            for (int i = 0; i < ghost.renderers.Length; i++)
            {
                Renderer renderer = ghost.renderers[i];
                if (renderer == null) continue;

                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

                // Un seul materiau partage par tous les fantomes ; la couleur passe par
                // un MaterialPropertyBlock, ce qui evite d'en dupliquer un par modele.
                int slots = renderer.sharedMaterials.Length;
                if (slots <= 1)
                {
                    renderer.sharedMaterial = ghostMaterial;
                }
                else
                {
                    Material[] materials = new Material[slots];
                    for (int m = 0; m < slots; m++) materials[m] = ghostMaterial;
                    renderer.sharedMaterials = materials;
                }
            }
        }

        private void Tint(Ghost ghost, Color color)
        {
            if (ghost.renderers == null || _colorPropertyId == 0) return;

            for (int i = 0; i < ghost.renderers.Length; i++)
            {
                Renderer renderer = ghost.renderers[i];
                if (renderer == null) continue;

                renderer.GetPropertyBlock(_block);
                _block.SetColor(_colorPropertyId, color);
                renderer.SetPropertyBlock(_block);
            }
        }

        private static void SetRenderersEnabled(Ghost ghost, bool enabled)
        {
            if (ghost.renderers == null) return;
            for (int i = 0; i < ghost.renderers.Length; i++)
                if (ghost.renderers[i] != null) ghost.renderers[i].enabled = enabled;
        }

        private int CountInUse()
        {
            int count = 0;
            for (int i = 0; i < _ghosts.Count; i++) if (_ghosts[i].inUse) count++;
            return count;
        }

        private void ReleaseAll()
        {
            for (int i = 0; i < _ghosts.Count; i++)
            {
                Ghost ghost = _ghosts[i];

                // L'apercu d'un conseil ne fait pas partie des menaces : ouvrir ou
                // fermer la vision ennemie ne doit pas l'effacer. Le joueur compare
                // justement sa reponse a la menace qu'il vient de regarder.
                if (ghost == _hint) continue;

                if (ghost.routine != null)
                {
                    StopCoroutine(ghost.routine);
                    ghost.routine = null;
                }

                ghost.inUse = false;

                if (ghost.gameObject != null) ghost.gameObject.SetActive(false);
            }

            ghostsShown = 0;
        }

        // =================================================================
        //  MATERIAU GENERE
        // =================================================================
        /// <summary>
        /// Materiau translucide par defaut, pour que la fonctionnalite marche sans
        /// aucune assignation dans l'inspecteur. Un materiau pose a la main dans
        /// ghostMaterial a toujours la priorite.
        /// </summary>
        private void EnsureMaterial()
        {
            if (ghostMaterial == null)
            {
                // ATTENTION, piege : NE PAS utiliser "Sprites/Default" ici. Ce shader
                // multiplie sa couleur par _RendererColor, une propriete que seul un
                // SpriteRenderer renseigne. Sur un MeshRenderer elle vaut (0,0,0,0),
                // donc le modele est rendu totalement transparent : on voyait le FX
                // d'apparition mais jamais le fantome.
                //
                // Standard en mode Fade est le choix sur en pipeline Built-in : il est
                // toujours present, il expose _Color avec son alpha, et il fonctionne
                // aussi bien sur un MeshRenderer que sur un SkinnedMeshRenderer.
                Shader shader = Shader.Find("Standard");

                if (shader != null)
                {
                    ghostMaterial = new Material(shader);
                    ConfigureStandardAsFade(ghostMaterial);
                }
                else
                {
                    // Replis, dans l'ordre : tous deux exposent _Color avec un alpha.
                    Shader fallback = Shader.Find("Legacy Shaders/Transparent/Diffuse");
                    if (fallback == null) fallback = Shader.Find("Particles/Standard Unlit");

                    if (fallback == null)
                    {
                        Debug.LogWarning("[ThreatPreview] Aucun shader transparent trouve : assigne un materiau dans ghostMaterial.");
                        return;
                    }

                    ghostMaterial = new Material(fallback);
                    ghostMaterial.renderQueue = 3100;
                }

                ghostMaterial.name = "ThreatPreviewGhost";
                Debug.LogFormat("[ThreatPreview] Materiau fantome cree a partir du shader \"{0}\".",
                                ghostMaterial.shader != null ? ghostMaterial.shader.name : "?");
            }

            if (ghostMaterial.HasProperty(ColorId)) _colorPropertyId = ColorId;
            else if (ghostMaterial.HasProperty(BaseColorId)) _colorPropertyId = BaseColorId;
            else _colorPropertyId = 0;

            if (_colorPropertyId == 0)
                Debug.LogWarning("[ThreatPreview] Le materiau fantome n'expose ni _Color ni _BaseColor : la teinte de posture ne s'appliquera pas.");
        }

        /// <summary>
        /// Bascule un materiau Standard en mode Fade. C'est la recette officielle du
        /// pipeline Built-in : changer le seul champ _Mode ne suffit pas, il faut aussi
        /// poser les modes de blend, couper l'ecriture de profondeur, activer le bon
        /// mot-cle de shader et deplacer le materiau dans la file transparente.
        /// </summary>
        private static void ConfigureStandardAsFade(Material material)
        {
            material.SetFloat("_Mode", 2f);   // 2 = Fade
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            // Un fantome ne doit pas briller ni refleter : il est lisse et mat.
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        }
    }
}

// ---------------------------------------------------------------------------
// NOTE D'OPTIMISATION
//
// 1. Update() se reduit a un ou deux Input.GetKey : aucun calcul par frame. Le calcul
//    des intentions n'a lieu que sur une action du joueur, jamais en boucle.
// 2. Les fantomes sont mis en pool PAR NIVEAU et simplement desactives. Le premier
//    affichage instancie, les suivants reutilisent : ouvrir et fermer l'apercu en
//    boucle ne produit plus aucun Instantiate ni Destroy, donc aucun pic de GC.
// 3. Un fantome est fabrique sous une racine DESACTIVEE. Un GameObject cree sous un
//    parent inactif ne declenche aucun Awake : aucun script clone (PawnController,
//    barre de vie, AudioSource) ne demarre, meme une frame.
// 4. Un seul materiau translucide est partage par tous les fantomes ; la couleur passe
//    par un MaterialPropertyBlock unique, ce qui evite une instance de materiau par
//    modele et garde un seul batch.
// 5. Les WaitForSeconds sont mises en cache et reconstruites uniquement quand une duree
//    change dans l'inspecteur ; sans cela chaque tour de boucle allouerait.
// 6. La boucle de deplacement n'alloue rien : "yield return null" ne cree pas d'objet,
//    et l'adoucissement est calcule sur place.
// 7. sqrMagnitude est prefere a Vector3.Distance pour les tests de vecteur nul.
// 8. Les fantomes sont sur la couche Ignore Raycast : ils n'entrent jamais dans le
//    Physics.RaycastNonAlloc de GameManager.
// ---------------------------------------------------------------------------
