using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MNLTHII;
using MNLTHII.Managers;
using MNLTHII.Rules;
using MNLTHII.UI;

namespace MNLTHII.Dev
{
    /// <summary>
    /// LE REALISATEUR DU TRAILER. Un outil de tournage, pas une feature du jeu.
    ///
    /// Il n'existe que si PlayerPrefs "mnlth_trailer" vaut 1 (menu Unity :
    /// Milchemet > Trailer > Activer le realisateur). En partie normale, rien de ce
    /// fichier ne tourne : la methode AutoCreate sort a la premiere ligne.
    ///
    /// Quand il est actif, la scene Game demarre SANS visite guidee, SANS
    /// enseignements au debut du tour et SANS sauvegarde (un magasin vide remplace
    /// le vrai), et chaque touche F joue une PRISE : le plateau est mis dans un
    /// etat precis (vraies regles, vrais prefabs, vraie camera), puis le tour se
    /// deroule tout seul. On lance le Recorder, on appuie sur la touche, on coupe.
    ///
    ///   F1   tour d'horizon : la camera fait le tour du plateau vide (12 s)
    ///   F2   la vague : trois emissaires sortent du Shofar est, puis marchent
    ///   F3   le Bunker : un rang 2 sur la colline est abat deux ennemis
    ///   F4   la bulle : deux ennemis frappent la bulle d'un Centre, qui se refait
    ///   F5   le Tank : menu de posture, creation, depart en assaut
    ///   F6   le bouclier : 3/4 fissures, le Tank abat le 4e, le Shofar est tombe au tour d'apres
    ///   F7   la chute : le Shofar ouest tombe et son secteur refleurit
    ///   F8   at'hapkha : la ruine ouest est retournee
    ///   F9   l'enseignement : deux textes, une question, la bonne reponse, la fissure
    ///   F10  l'ecran de fin, Tzadik ve'Tov Lo
    ///   F11  TOUT ENCHAINER : les dix prises a la suite, un noir d'une seconde entre
    ///        deux, le temps de chaque prise ecrit dans la console. Une seule touche,
    ///        un seul enregistrement, et on coupe sur les noirs.
    ///   F12  tout remettre a zero (la scene est rechargee, le realisateur revient)
    ///   H    prises PROPRES (sans HUD) ou prises avec le HUD
    ///
    /// PRISES PROPRES (par defaut) : tout l'habillage disparait de l'image. Les
    /// plaques du HUD, la barre de phase, le bandeau d'etape, l'ouverture du tour,
    /// les barres de vie et les marques de posture sont eteints a chaque image ;
    /// seuls restent le plateau, les pions, les effets, les chiffres qui flottent
    /// et, pour les prises qui parlent d'eux, le choix du Tank, l'enseignement et
    /// l'ecran de fin. Rien n'est detruit : H rallume tout.
    ///
    /// La bobine ecrit ses temps de coupe dans Trailer/timecodes.txt, a cote du
    /// dossier Assets, en plus de la console.
    ///
    /// Les prises s'enchainent sur le meme plateau : F6 puis F7 puis F8 forment une
    /// suite. F12 entre deux prises pour repartir propre.
    /// </summary>
    public class TrailerDirector : MonoBehaviour
    {
        public const string PrefKey = "mnlth_trailer";
        public const string CleanPrefKey = "mnlth_trailer_clean";

        public static TrailerDirector Instance { get; private set; }

        private bool _busy;
        private bool _blackout;
        private GameObject _blackCard;
        private float _reelStart;
        private string _status = "";
        private GUIStyle _style;

        // Prises propres : ce qui a ete eteint, pour tout rallumer avec H.
        private bool _clean;
        private float _nextSweep;
        private readonly List<Canvas> _mutedCanvases = new List<Canvas>();
        private readonly List<CanvasGroup> _mutedGroups = new List<CanvasGroup>();
        private readonly List<string> _timecodes = new List<string>();

        /// <summary>Sous le HUD, ce qui reste visible en prise propre : le sujet de la prise.</summary>
        private static readonly string[] KeepOnHud = { "ChoixDuTank", "Enseignement", "FinDePartie", "Impact" };

        // =================================================================
        //  NAISSANCE
        // =================================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (PlayerPrefs.GetInt(PrefKey, 0) != 1) return;

            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            Spawn();
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                                          UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (PlayerPrefs.GetInt(PrefKey, 0) != 1) return;
            Spawn();
        }

        private static void Spawn()
        {
            if (Instance != null) return;
            if (LocalGameEngine.Instance == null) return;   // pas la scene de jeu
            new GameObject("Realisateur du trailer").AddComponent<TrailerDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Pas de visite, pas d'enseignement automatique, pas de sauvegarde.
            if (LocalGameEngine.Instance != null) LocalGameEngine.Instance.playIntroTour = false;
            TeachingPanel.AutoSkip = true;
            TeachingPanel.ForceQuestion = false;
            SaveManager.Store = new NullStore();

            _clean = PlayerPrefs.GetInt(CleanPrefKey, 1) == 1;

            Debug.Log("[Trailer] Realisateur actif. F1..F10 : prises, F11 : bobine, F12 : remise a zero, H : HUD. Prises "
                      + (_clean ? "PROPRES (sans HUD)." : "avec le HUD."));
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            RestoreUi();
        }

        /// <summary>
        /// Prise propre : a chaque image, l'habillage est eteint. Un balayage toutes
        /// les 0,25 s ramasse ce qui vient de naitre (barres de vie, marques, bandeaux) ;
        /// entre deux balayages, on maintient seulement ce qu'on connait deja.
        /// </summary>
        private void LateUpdate()
        {
            if (!_clean) return;

            if (Time.unscaledTime >= _nextSweep)
            {
                _nextSweep = Time.unscaledTime + 0.25f;
                Sweep();
            }

            for (int i = _mutedCanvases.Count - 1; i >= 0; i--)
            {
                Canvas c = _mutedCanvases[i];
                if (c == null) { _mutedCanvases.RemoveAt(i); continue; }
                if (c.enabled) c.enabled = false;
            }
            for (int i = _mutedGroups.Count - 1; i >= 0; i--)
            {
                CanvasGroup g = _mutedGroups[i];
                if (g == null) { _mutedGroups.RemoveAt(i); continue; }
                if (g.alpha != 0f) { g.alpha = 0f; g.blocksRaycasts = false; g.interactable = false; }
            }
        }

        private void Sweep()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null) continue;
                if (_blackCard != null && canvas.gameObject == _blackCard) continue;
                // Les canvas emboites (sous-canvas) suivent leur parent : on ne traite que les racines.
                if (!canvas.isRootCanvas) continue;

                // Le HUD principal : on eteint bloc par bloc, le sujet de la prise reste.
                if (canvas.GetComponent<HudController>() != null)
                {
                    MuteHudBlocks(canvas.transform);
                    continue;
                }

                // Les chiffres qui flottent (degats, fissures) font partie de l'action : on les garde.
                Transform parent = canvas.transform.parent;
                if (parent != null && parent.name == "DamagePopups") continue;
                if (canvas.GetComponent<DamagePopup>() != null) continue;

                if (!_mutedCanvases.Contains(canvas)) _mutedCanvases.Add(canvas);
            }
        }

        private void MuteHudBlocks(Transform canvasRoot)
        {
            for (int i = 0; i < canvasRoot.childCount; i++)
            {
                Transform child = canvasRoot.GetChild(i);
                if (child.name == "HUD")
                {
                    for (int j = 0; j < child.childCount; j++)
                    {
                        Transform block = child.GetChild(j);
                        if (System.Array.IndexOf(KeepOnHud, block.name) >= 0) continue;
                        MuteGroup(block);
                    }
                }
                else
                {
                    MuteGroup(child);   // la barre de la vue immersive, et tout autre habillage pose a cote
                }
            }
        }

        private void MuteGroup(Transform t)
        {
            CanvasGroup g = t.GetComponent<CanvasGroup>();
            if (g == null) g = t.gameObject.AddComponent<CanvasGroup>();
            if (!_mutedGroups.Contains(g)) _mutedGroups.Add(g);
        }

        /// <summary>Tout rallumer : les canvas eteints et les blocs du HUD.</summary>
        private void RestoreUi()
        {
            for (int i = 0; i < _mutedCanvases.Count; i++)
                if (_mutedCanvases[i] != null) _mutedCanvases[i].enabled = true;
            _mutedCanvases.Clear();

            for (int i = 0; i < _mutedGroups.Count; i++)
            {
                CanvasGroup g = _mutedGroups[i];
                if (g == null) continue;
                g.alpha = 1f;
                g.blocksRaycasts = true;
                g.interactable = true;
            }
            _mutedGroups.Clear();
        }

        private void ToggleClean()
        {
            _clean = !_clean;
            PlayerPrefs.SetInt(CleanPrefKey, _clean ? 1 : 0);
            PlayerPrefs.Save();
            if (_clean) _nextSweep = 0f;
            else RestoreUi();
            Debug.Log("[Trailer] Prises " + (_clean ? "PROPRES : le HUD est eteint." : "avec le HUD."));
        }

        // =================================================================
        //  TOUCHES
        // =================================================================
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.H)) ToggleClean();
            if (Input.GetKeyDown(KeyCode.F12)) { Reset(); return; }

            if (_busy) return;

            if (Input.GetKeyDown(KeyCode.F1)) Run(ShotOrbit(), "F1 tour d'horizon");
            else if (Input.GetKeyDown(KeyCode.F2)) Run(ShotWave(), "F2 la vague");
            else if (Input.GetKeyDown(KeyCode.F3)) Run(ShotBunker(), "F3 le Bunker");
            else if (Input.GetKeyDown(KeyCode.F4)) Run(ShotBubble(), "F4 la bulle");
            else if (Input.GetKeyDown(KeyCode.F5)) Run(ShotTank(), "F5 le Tank");
            else if (Input.GetKeyDown(KeyCode.F6)) Run(ShotShieldBreak(), "F6 le bouclier");
            else if (Input.GetKeyDown(KeyCode.F7)) Run(ShotFall(), "F7 la chute");
            else if (Input.GetKeyDown(KeyCode.F8)) Run(ShotTurnRuin(), "F8 at'hapkha");
            else if (Input.GetKeyDown(KeyCode.F9)) Run(ShotTeaching(), "F9 l'enseignement");
            else if (Input.GetKeyDown(KeyCode.F10)) Run(ShotGameOver(), "F10 la fin");
            else if (Input.GetKeyDown(KeyCode.F11)) StartCoroutine(Reel());
        }

        /// <summary>
        /// F11 : la bobine complete. Chaque prise est precedee d'un noir d'une seconde
        /// (le point de coupe) et son heure de debut est ecrite dans la console, en
        /// secondes depuis le debut de la bobine : de quoi retrouver chaque plan dans
        /// l'enregistrement sans le relire en entier.
        /// </summary>
        private IEnumerator Reel()
        {
            if (_busy) yield break;
            _busy = true;
            _reelStart = Time.realtimeSinceStartup;
            _timecodes.Clear();
            Note("0.0 s  depart de la bobine (prises " + (_clean ? "propres" : "avec HUD") + ")");

            yield return StartCoroutine(Take("1 tour d'horizon", ShotOrbit()));
            yield return StartCoroutine(Take("2 le Tank", ShotTank()));
            yield return StartCoroutine(Take("3 le Bunker", ShotBunker()));
            yield return StartCoroutine(Take("4 la vague", ShotWave()));
            yield return StartCoroutine(Take("5 la bulle", ShotBubble()));
            yield return StartCoroutine(Take("6 le bouclier et la chute", ShotShieldBreak()));
            yield return StartCoroutine(Take("7 la chute a l'ouest", ShotFall()));
            yield return StartCoroutine(Take("8 at'hapkha", ShotTurnRuin()));
            yield return StartCoroutine(Take("9 l'enseignement", ShotTeaching()));
            yield return StartCoroutine(Take("10 la fin", ShotGameOver()));

            yield return StartCoroutine(Blackout(2.0f));
            Note(string.Format("{0:0.0} s  fin de la bobine", Time.realtimeSinceStartup - _reelStart));
            WriteTimecodes();
            _status = "";
            _busy = false;
        }

        private void Note(string line)
        {
            _timecodes.Add(line);
            Debug.Log("[Trailer] " + line);
        }

        /// <summary>Trailer/timecodes.txt a cote du dossier Assets (ou dans les donnees du jeu, hors editeur).</summary>
        private void WriteTimecodes()
        {
            try
            {
                string dir = System.IO.Path.Combine(Application.dataPath, "../Trailer");
                if (!Application.isEditor) dir = System.IO.Path.Combine(Application.persistentDataPath, "Trailer");
                System.IO.Directory.CreateDirectory(dir);
                string path = System.IO.Path.Combine(dir, "timecodes.txt");
                System.IO.File.WriteAllLines(path, _timecodes.ToArray());
                Debug.Log("[Trailer] Temps de coupe ecrits dans " + System.IO.Path.GetFullPath(path));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Trailer] Temps de coupe non ecrits : " + e.Message);
            }
        }

        private IEnumerator Take(string name, IEnumerator shot)
        {
            yield return StartCoroutine(Blackout(1.2f));
            yield return StartCoroutine(WaitReady());
            _status = name;
            Note(string.Format("{0:0.0} s  prise {1}", Time.realtimeSinceStartup - _reelStart, name));
            yield return StartCoroutine(shot);
            yield return Wait(1.0f);
            Note(string.Format("{0:0.0} s  fin de la prise {1}", Time.realtimeSinceStartup - _reelStart, name));
        }

        private IEnumerator Blackout(float seconds)
        {
            SetBlack(true);
            yield return Wait(seconds);
            SetBlack(false);
        }

        /// <summary>
        /// Le carton noir est un vrai Canvas au-dessus de tout : le Recorder le voit a
        /// coup sur, la ou un dessin OnGUI pouvait lui echapper.
        /// </summary>
        private void SetBlack(bool on)
        {
            _blackout = on;
            if (_blackCard == null)
            {
                _blackCard = new GameObject("Noir du trailer", typeof(Canvas), typeof(UnityEngine.UI.Image));
                Canvas canvas = _blackCard.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32000;
                UnityEngine.UI.Image img = _blackCard.GetComponent<UnityEngine.UI.Image>();
                img.color = Color.black;
                img.raycastTarget = true;
                _blackCard.SetActive(false);
            }
            if (_blackCard.activeSelf != on) _blackCard.SetActive(on);
        }

        private void Run(IEnumerator shot, string name)
        {
            StartCoroutine(Wrap(shot, name));
        }

        private IEnumerator Wrap(IEnumerator shot, string name)
        {
            _busy = true;
            _status = name;
            Debug.Log("[Trailer] " + name);

            yield return StartCoroutine(WaitReady());
            yield return StartCoroutine(shot);

            _status = "";
            _busy = false;
        }

        private void Reset()
        {
            TeachingPanel.AutoSkip = true;
            TeachingPanel.ForceQuestion = false;
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        /// <summary>L'aide a l'ecran, seulement entre deux prises : le Recorder ne la voit pas pendant une prise.</summary>
        private void OnGUI()
        {
            if (_blackout || _busy) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label);
                _style.fontSize = 16;
                _style.normal.textColor = new Color(1f, 0.85f, 0.4f);
            }

            GUI.Label(new Rect(16, 12, 1200, 30), "TRAILER  F11 = TOUT ENCHAINER   |   F1 orbite  F2 vague  F3 bunker  F4 bulle  F5 tank  F6 bouclier  F7 chute  F8 ruine  F9 lecon  F10 fin  F12 reset  H hud (" + (_clean ? "prises propres" : "avec HUD") + ")", _style);
            if (!string.IsNullOrEmpty(_status)) GUI.Label(new Rect(16, 36, 600, 30), _status, _style);
        }

        // =================================================================
        //  OUTILS
        // =================================================================
        private static BoardController Board { get { return BoardController.instance; } }

        private static Hexagon Hex(int q, int r)
        {
            if (Board == null) return null;
            return Board.getHexByCoord(new HexCoord(q, r, -q - r));
        }

        private static Hexagon Corner(int index)
        {
            int q, r, s;
            MapGenerator.GetPortalCorner(index, out q, out r, out s);
            return Hex(q, r);
        }

        /// <summary>Le plateau est pret et le joueur a la main (phase de depense).</summary>
        private IEnumerator WaitReady()
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deadline)
            {
                TurnManager turns = TurnManager.Instance;
                if (turns != null && turns.phase == TurnPhase.Spending && Board != null
                    && Board.HexagonsInBoard != null && Board.HexagonsInBoard.Count > 100
                    && !IntroTour.Running) yield break;
                yield return null;
            }
            Debug.LogWarning("[Trailer] Le plateau n'est pas pret (phase " + (TurnManager.Instance != null ? TurnManager.Instance.phase.ToString() : "?") + ").");
        }

        /// <summary>Joue un tour complet (Tanks, ennemis, batiments, Shofars, floraison) et rend la main.</summary>
        private IEnumerator PlayTurn()
        {
            TurnManager turns = TurnManager.Instance;
            if (turns == null) yield break;

            int before = turns.currentTurn;
            turns.EndPlayerTurn();

            float deadline = Time.realtimeSinceStartup + 120f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (turns.gameOver) yield break;
                if (turns.phase == TurnPhase.Spending && turns.currentTurn > before) yield break;
                yield return null;
            }
        }

        private static void GiveEnergy(int amount)
        {
            if (EnergyManager.Instance != null) EnergyManager.Instance.ResetEnergy(amount);
        }

        /// <summary>Un batiment pose d'un coup, gratuit. Rend le NOUVEL hexagone (l'ancien est detruit).</summary>
        private static Hexagon Build(int q, int r, int level)
        {
            Hexagon hex = Hex(q, r);
            if (hex == null) return null;
            InteractionRules.UpgradeBuilding(hex, level);
            return Hex(q, r);
        }

        private static PawnController Tank(int q, int r, PawnStance stance, int level)
        {
            if (Board == null) return null;

            Hexagon hex = Hex(q, r);
            PawnController tank = (hex != null) ? Board.SpawnUnitVisual(hex, 1, "unit") : null;

            for (int n = 0; tank == null && n < 6; n++)
            {
                Hexagon other = Hex(q + Neighbours[n, 0], r + Neighbours[n, 1]);
                if (other == null || !InteractionRules.IsHexWalkable(other)) continue;
                tank = Board.SpawnUnitVisual(other, 1, "unit");
            }
            if (tank == null) return null;

            tank.SetStance(stance);
            if (level >= 2) tank = Board.UpgradePawnVisual(tank, 2);
            return tank;
        }

        private static readonly int[,] Neighbours =
        {
            { 1, 0 }, { 1, -1 }, { 0, -1 }, { -1, 0 }, { -1, 1 }, { 0, 1 }
        };

        private static PawnController Enemy(int q, int r, int level, int portalIndex)
        {
            if (Board == null) return null;

            // La case voulue, sinon la premiere voisine libre : quand les prises
            // s'enchainent, un pion d'une prise precedente peut etre reste la.
            Hexagon hex = Hex(q, r);
            PawnController enemy = (hex != null) ? Board.SpawnUnitVisual(hex, level, "enemy") : null;

            for (int n = 0; enemy == null && n < 6; n++)
            {
                Hexagon other = Hex(q + Neighbours[n, 0], r + Neighbours[n, 1]);
                if (other == null || !InteractionRules.IsHexWalkable(other)) continue;
                enemy = Board.SpawnUnitVisual(other, level, "enemy");
            }
            if (enemy == null) return null;

            int cq, cr, cs;
            MapGenerator.GetPortalCorner(portalIndex, out cq, out cr, out cs);
            enemy.SetOriginPortal(new HexCoord(cq, cr, cs));

            if (FXManager.Instance != null) FXManager.Instance.SpawnEnemyFX(enemy.transform.position);
            return enemy;
        }

        private static void Focus(Hexagon hex, float factor)
        {
            if (hex == null || CameraDirector.Instance == null) return;
            CameraDirector.Instance.FocusOn(hex.transform.position, factor);
        }

        private static void ReleaseCamera()
        {
            if (CameraDirector.Instance != null) CameraDirector.Instance.Release();
        }

        private static void SetHud(bool visible)
        {
            ImmersiveCamera immersive = ImmersiveCamera.Instance;
            if (immersive == null || immersive.hudGroup == null) return;

            immersive.hudGroup.alpha = visible ? 1f : 0f;
            immersive.hudGroup.blocksRaycasts = visible;
            immersive.hudGroup.interactable = visible;
        }

        private static WaitForSecondsRealtime Wait(float seconds)
        {
            return new WaitForSecondsRealtime(seconds);
        }

        // =================================================================
        //  LES PRISES
        // =================================================================
        /// <summary>F1 : un tour complet de camera autour du plateau, 12 secondes.</summary>
        private IEnumerator ShotOrbit()
        {
            CameraDirector director = CameraDirector.Instance;
            Camera cam = Camera.main;
            Hexagon center = Hex(0, 0);
            if (director == null || cam == null || center == null) yield break;

            SetHud(false);

            Vector3 pivot = center.transform.position;
            Vector3 start = cam.transform.position;
            Vector3 flat = new Vector3(start.x - pivot.x, 0f, start.z - pivot.z);
            float radius = Mathf.Max(4f, flat.magnitude * 0.92f);
            float height = (start.y - pivot.y) * 0.85f;
            float angle0 = Mathf.Atan2(flat.z, flat.x);

            director.RequestPerspective(38f, 0.6f);
            yield return Wait(0.6f);
            director.BeginOverride();

            const float duration = 12f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                // Lent au depart et a l'arrivee : un mouvement de grue, pas un tourniquet.
                float eased = k * k * (3f - 2f * k);
                float angle = angle0 + eased * Mathf.PI * 2f;
                Vector3 pos = pivot + new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
                director.SetCameraPose(pos, Quaternion.LookRotation(pivot - pos, Vector3.up));
                yield return null;
            }

            director.EndOverride(1.2f);
            yield return Wait(1.4f);
            SetHud(true);
        }

        /// <summary>F2 : trois emissaires sortent du Shofar est, puis la phase ennemie les fait marcher.</summary>
        private IEnumerator ShotWave()
        {
            GiveEnergy(180);
            Hexagon portal = Corner(0);
            if (portal == null || PortalManager.Instance == null) yield break;

            Focus(portal, 0.5f);
            yield return Wait(1.2f);

            int[] levels = { 1, 1, 2 };
            for (int i = 0; i < levels.Length; i++)
            {
                PortalManager.Instance.SpawnEnemyFromPortal(portal, TurnManager.Instance.currentTurn, levels[i]);
                yield return Wait(0.7f);
            }

            yield return Wait(1.0f);
            ReleaseCamera();
            yield return Wait(0.8f);

            yield return StartCoroutine(PlayTurn());
        }

        /// <summary>F3 : un Bunker rang 2 sur la colline est, deux ennemis a portee, le tour joue.</summary>
        private IEnumerator ShotBunker()
        {
            GiveEnergy(180);
            Hexagon bunker = Build(3, 0, 2);
            Enemy(5, 0, 1, 0);
            Enemy(4, -1, 1, 0);

            if (bunker != null) Focus(bunker, 0.55f);
            yield return Wait(1.6f);
            ReleaseCamera();
            yield return Wait(0.8f);

            yield return StartCoroutine(PlayTurn());
        }

        /// <summary>F4 : un Centre rang 2 et sa bulle, deux ennemis de rang 2 qui la frappent, puis elle se refait.</summary>
        private IEnumerator ShotBubble()
        {
            GiveEnergy(180);
            Hexagon center = Build(5, -4, 2);
            Enemy(6, -6, 2, 1);
            Enemy(7, -5, 2, 1);

            if (center != null) Focus(center, 0.5f);
            yield return Wait(1.8f);
            ReleaseCamera();
            yield return Wait(0.8f);

            yield return StartCoroutine(PlayTurn());
            yield return StartCoroutine(PlayTurn());
        }

        /// <summary>F5 : le menu de posture s'ouvre sur une plaine, le Tank est cree en assaut et part.</summary>
        private IEnumerator ShotTank()
        {
            GiveEnergy(180);
            Hexagon plain = Hex(2, 0);
            TurnManager turns = TurnManager.Instance;
            if (plain == null || turns == null) yield break;

            Focus(plain, 0.5f);
            yield return Wait(1.0f);

            // Le vrai menu, tel que le joueur le voit.
            turns.ExecuteSpendingAction(plain);
            yield return Wait(2.4f);

            if (TankChoicePanel.Instance != null) TankChoicePanel.Instance.Hide();
            InteractionRules.CreateTankWithStance(plain, PawnStance.Assault);
            yield return Wait(1.6f);

            ReleaseCamera();
            yield return Wait(0.8f);

            yield return StartCoroutine(PlayTurn());
        }

        /// <summary>
        /// F6 : le Shofar est a une fissure de ceder. Un Tank rang 2 abat l'emissaire
        /// qui manque : le bouclier se brise. Au tour suivant, le Tank frappe a plein
        /// et le Shofar tombe ; son secteur refleurit.
        /// </summary>
        private IEnumerator ShotShieldBreak()
        {
            GiveEnergy(180);
            Hexagon portal = Corner(0);
            PortalManager portals = PortalManager.Instance;
            if (portal == null || portals == null) yield break;

            // En Chasse d'abord : le Tank abat l'emissaire a coup sur. En Assaut
            // ensuite, pour frapper le Shofar des que son bouclier est tombe.
            PawnController tank = Tank(5, 0, PawnStance.Hunt, 2);
            Enemy(6, 0, 1, 0);

            portals.RestorePortal(portal.positionInTheBoard, InteractionRules.PORTAL_KILLS_TO_BREAK_SHIELD - 1, 0, 0);
            // Juste au-dessus du plancher : le coup a plein le fera tomber.
            portal.currentHP = Mathf.Min(portal.currentHP, 22);
            if (Board != null) Board.RefreshStructureHealthBars();

            Focus(portal, 0.5f);
            yield return Wait(1.6f);
            ReleaseCamera();
            yield return Wait(0.8f);

            yield return StartCoroutine(PlayTurn());   // le 4e emissaire tombe : bouclier brise

            if (tank != null) tank.SetStance(PawnStance.Assault);
            yield return Wait(0.6f);

            yield return StartCoroutine(PlayTurn());   // le Tank frappe a plein : le Shofar tombe, la terre refleurit
        }

        /// <summary>F7 : le Shofar ouest, bouclier deja rompu, tombe sous un Tank rang 2 ; floraison.</summary>
        private IEnumerator ShotFall()
        {
            GiveEnergy(180);
            Hexagon portal = Corner(3);
            PortalManager portals = PortalManager.Instance;
            if (portal == null || portals == null) yield break;

            Tank(-5, 0, PawnStance.Assault, 2);
            portals.RestorePortal(portal.positionInTheBoard, InteractionRules.PORTAL_KILLS_TO_BREAK_SHIELD,
                                  InteractionRules.PORTAL_SHIELD_DOWN_TURNS, 0);
            portal.currentHP = Mathf.Min(portal.currentHP, 18);
            if (Board != null) Board.RefreshStructureHealthBars();

            Focus(portal, 0.5f);
            yield return Wait(1.6f);
            ReleaseCamera();
            yield return Wait(0.8f);

            yield return StartCoroutine(PlayTurn());
        }

        /// <summary>F8 : la ruine ouest est retournee (at'hapkha). Si le Shofar est encore debout, il tombe d'abord sans ceremonie.</summary>
        private IEnumerator ShotTurnRuin()
        {
            GiveEnergy(300);
            Hexagon ruin = Corner(3);
            if (ruin == null) yield break;

            if (ruin.type == TypeOfHex.portal)
            {
                InteractionRules.DestroyBuilding(ruin);
                yield return Wait(0.5f);
                if (LandBloom.Instance != null) yield return StartCoroutine(LandBloom.Instance.ProcessBlooms());
                ruin = Corner(3);
            }

            // Un Tank a cote : c'est lui qui porte la construction jusque-la.
            Tank(-6, 0, PawnStance.Guard, 1);

            Focus(ruin, 0.5f);
            yield return Wait(1.8f);

            InteractionRules.TurnPortal(ruin);
            yield return Wait(2.2f);

            ReleaseCamera();
            yield return Wait(1.0f);
        }

        /// <summary>F9 : la carte d'enseignement, avec une question, repondue juste.</summary>
        private IEnumerator ShotTeaching()
        {
            TeachingPanel panel = TeachingPanel.Instance;
            TurnManager turns = TurnManager.Instance;
            if (panel == null || turns == null) yield break;

            // Une question n'est posee qu'un tour sur deux : on passe un tour pair.
            int turn = turns.currentTurn;
            if (turn % 2 != 0) turn++;

            TeachingPanel.AutoSkip = false;
            TeachingPanel.ForceQuestion = true;
            Coroutine play = StartCoroutine(panel.Play(turn));

            // Deux textes : on laisse lire, puis on passe.
            yield return Wait(4.5f);
            if (panel.nextButton != null) panel.nextButton.onClick.Invoke();
            yield return Wait(4.5f);
            if (panel.nextButton != null) panel.nextButton.onClick.Invoke();

            // La question : on lit, puis la bonne case.
            yield return Wait(3.5f);
            int slot = panel.CorrectSlot;
            if (panel.answerButtons != null && slot >= 0 && slot < panel.answerButtons.Length && panel.answerButtons[slot] != null)
                panel.answerButtons[slot].onClick.Invoke();

            yield return Wait(4.0f);
            if (panel.nextButton != null) panel.nextButton.onClick.Invoke();
            yield return play;

            TeachingPanel.ForceQuestion = false;
            TeachingPanel.AutoSkip = true;
        }

        /// <summary>F10 : tous les Shofars retournes, l'ecran de fin au rang supreme.</summary>
        private IEnumerator ShotGameOver()
        {
            if (Board == null) yield break;

            for (int i = 0; i < 6; i++)
            {
                Hexagon corner = Corner(i);
                if (corner == null) continue;

                if (corner.type == TypeOfHex.portal) InteractionRules.DestroyBuilding(corner);
                corner = Corner(i);
                if (corner != null && corner.type == TypeOfHex.Destroyed && corner.level < 1)
                {
                    corner.level = 1;
                    corner.buildingType = "Prod";
                    Board.UpgradeHexVisual(corner);
                }
            }

            yield return Wait(1.0f);
            if (GameOverPanel.Instance != null) GameOverPanel.Instance.Show(true);
            yield return Wait(1.0f);
        }

        // =================================================================
        //  UN MAGASIN DE SAUVEGARDE QUI N'ECRIT RIEN
        // =================================================================
        private class NullStore : ISaveStore
        {
            private readonly List<string> _empty = new List<string>(0);
            public List<string> ReadAll() { return _empty; }
            public void Write(string id, string json) { }
            public void Delete(string id) { }
        }
    }
}
