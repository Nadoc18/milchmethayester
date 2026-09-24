using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using MNLTHII;
using MNLTHII.Rules;

namespace MNLTHII.Managers
{
    /// <summary>
    /// LE BOUCLIER ET LES FISSURES, VISIBLES SUR LE PLATEAU.
    ///
    /// Jusqu'ici l'etat d'un Shofar ne se lisait que dans un panneau au survol : rien
    /// sur le plateau lui-meme ne disait qu'il etait protege, ni qu'on etait en train
    /// de le fissurer. Le coeur du jeu se jouait hors de vue.
    ///
    /// Sur chaque Shofar :
    ///
    ///   BOUCLIER INTACT   une bulle lumineuse l'entoure. Plus il est fissure, plus
    ///                     elle faiblit - on la voit s'eteindre au fil des morts.
    ///   FISSURES          quatre pastilles au-dessus du Shofar. Chaque ennemi issu
    ///                     de CE Shofar que tu abats en allume une en orange. A quatre,
    ///                     le bouclier cede.
    ///   BOUCLIER ROMPU    la bulle disparait ; les pastilles passent au rouge et
    ///                     clignotent. Leur nombre = les tours qui restent pour frapper
    ///                     (4, 3, 2, 1). C'est LE moment d'envoyer tes Tanks.
    ///
    /// Se cree tout seul au lancement, rien a poser dans la scene. Decoche "active"
    /// pour tout masquer.
    ///
    /// Note d'optimisation : l'etat des Shofars est relu cinq fois par seconde, pas a
    /// chaque frame. Chaque frame, seulement l'orientation des pastilles vers la camera
    /// et le clignotement : six transforms, un MaterialPropertyBlock reutilise, aucune
    /// allocation. Les objets sont crees une fois par Shofar.
    /// </summary>
    public class PortalStatusVisual : MonoBehaviour
    {
        public static PortalStatusVisual Instance;

        [Header("Interrupteur")]
        public bool active = true;

        [Header("Bulle du bouclier")]
        /// <summary>
        /// ORANGE, pas bleu. Le bleu clair du depart se confondait avec le ciel et avec
        /// les effets allies : on ne voyait pas qu'un Shofar etait protege. L'orange est
        /// la couleur de la menace dans tout le reste du jeu.
        /// </summary>
        public Color shieldColor = new Color(1f, 0.58f, 0.18f, 1f);

        /// <summary>
        /// La bulle du joueur, sur un Centre de Commandement. CYAN, la couleur alliee de
        /// tout le reste du jeu - le compteur d'Energie, les Tanks, la jauge de la Base.
        /// Deux bulles de la meme couleur sur le meme plateau, et on ne saurait plus
        /// laquelle il faut percer et laquelle il faut defendre.
        /// </summary>
        public Color commandShieldColor = new Color(0.30f, 0.94f, 0.86f, 1f);

        [Tooltip("Le disque pose au sol qui montre les cases interdites, sous la bulle d'un Centre.")]
        public Color commandZoneColor = new Color(0.30f, 0.94f, 0.86f, 0.22f);

        [Range(0f, 4f)] public float shieldStrength = 1.4f;
        [Range(0.5f, 8f)] public float shieldPower = 2.2f;
        [Tooltip("Taille de la bulle par rapport au Shofar.")]
        public float shieldScale = 1.25f;

        [Header("Pastilles de fissure")]
        [Tooltip("Hauteur des pastilles au-dessus du pied du Shofar (la barre de vie est a 1.3).")]
        public float pipHeight = 1.05f;
        public float pipSize = 0.11f;
        public float pipSpacing = 0.15f;
        public Color pipEmpty = new Color(1f, 1f, 1f, 0.28f);
        // Les fissures passent au jaune : l'orange est maintenant la couleur de la
        // bulle, et deux oranges cote a cote ne se distinguent plus.
        public Color pipCracked = new Color(1f, 0.85f, 0.30f, 1f);
        public Color pipBroken = new Color(1f, 0.28f, 0.25f, 1f);
        public float brokenPulseSpeed = 5f;

        [Tooltip("Relecture de l'etat des Shofars, en secondes.")]
        public float refreshInterval = 0.2f;

        // =================================================================
        //  ETAT
        // =================================================================
        private class Visual
        {
            public Hexagon portal;
            public Transform root;
            public Renderer dome;
            public Transform pipRow;
            public Renderer[] pips;
            public bool broken;

            /// <summary>Le disque au sol d'un Centre : les cases que la bulle interdit.</summary>
            public Renderer zone;
        }

        private readonly List<Visual> _visuals = new List<Visual>(8);
        private readonly List<Renderer> _scratch = new List<Renderer>(8);

        private Material _domeMaterial;
        private Material _pipMaterial;
        private static Mesh _quad;
        private static Texture2D _discTex;

        private MaterialPropertyBlock _block;
        private Transform _camTransform;
        private float _nextRefresh;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int RimPowerId = Shader.PropertyToID("_RimPower");
        private static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");

        // =================================================================
        //  CYCLE DE VIE
        // =================================================================
        // Le composant vit dans la scene chargee : il disparait avec elle. Sans ce
        // rappel, il n'etait cree que pour la PREMIERE scene - depuis le menu
        // principal (ou apres "Rejouer"), la scene de jeu n'en avait plus, et les
        // boucliers des Shofars ne s'affichaient plus.
        private static bool _sceneHooked;

        private static void HookScenes()
        {
            if (_sceneHooked) return;
            _sceneHooked = true;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                                          UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            Bootstrap();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            HookScenes();
            if (Instance != null) return;
            new GameObject("PortalStatusVisual (auto)").AddComponent<PortalStatusVisual>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            _block = new MaterialPropertyBlock();

            Shader rim = Resources.Load<Shader>("Shaders/MNLTH_PawnRim");
            Shader decal = Resources.Load<Shader>("Shaders/MNLTH_GroundDecal");

            if (rim != null)
            {
                _domeMaterial = new Material(rim) { name = "Bouclier de Shofar" };
                _domeMaterial.SetColor(RimColorId, shieldColor);
                _domeMaterial.SetFloat(RimPowerId, shieldPower);
                _domeMaterial.SetFloat(RimStrengthId, shieldStrength);
            }

            if (decal != null)
            {
                _pipMaterial = new Material(decal) { name = "Pastilles de fissure" };
                _pipMaterial.SetTexture(MainTexId, DiscTexture());
                _pipMaterial.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
                _pipMaterial.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
                _pipMaterial.SetColor(ColorId, Color.white);
                // Au-dessus du reste : les pastilles ne doivent pas disparaitre derriere le Shofar.
                _pipMaterial.renderQueue = 3100;
            }

            if (rim == null || decal == null)
                Debug.LogWarning("[PortalStatus] Shaders introuvables dans Resources/Shaders : bouclier et fissures non affiches.");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            for (int i = 0; i < _visuals.Count; i++)
                if (_visuals[i] != null && _visuals[i].root != null) Destroy(_visuals[i].root.gameObject);
            _visuals.Clear();

            if (_domeMaterial != null) Destroy(_domeMaterial);
            if (_pipMaterial != null) Destroy(_pipMaterial);
        }

        // =================================================================
        //  BOUCLE
        // =================================================================
        private void LateUpdate()
        {
            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + refreshInterval;
                Refresh();
            }

            if (_camTransform == null)
            {
                Camera cam = Camera.main;
                if (cam != null) _camTransform = cam.transform;
            }

            // Les pastilles font face a la camera, et clignotent quand le bouclier est rompu.
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * brokenPulseSpeed);

            for (int i = 0; i < _visuals.Count; i++)
            {
                Visual v = _visuals[i];
                if (v == null || v.pipRow == null || !v.pipRow.gameObject.activeSelf) continue;

                if (_camTransform != null) v.pipRow.rotation = _camTransform.rotation;

                if (!v.broken) continue;

                for (int p = 0; p < v.pips.Length; p++)
                {
                    Renderer r = v.pips[p];
                    if (r == null || !r.enabled) continue;

                    Color c = pipBroken;
                    c.a *= pulse;
                    SetColor(r, c);
                }
            }
        }

        /// <summary>Relit l'etat de chaque Shofar et met la bulle et les pastilles a jour.</summary>
        private void Refresh()
        {
            BoardController board = BoardController.instance;
            PortalManager portals = PortalManager.Instance;
            TurnManager turns = TurnManager.Instance;

            bool show = active && board != null && portals != null && board.HexagonsInBoard != null
                        && _domeMaterial != null && _pipMaterial != null
                        && (turns == null || !turns.gameOver);

            // Shofars disparus (detruits, remplaces par leur epave) : on nettoie.
            for (int i = _visuals.Count - 1; i >= 0; i--)
            {
                Visual v = _visuals[i];
                if (v.portal == null || !CarriesShield(v.portal))
                {
                    if (v.root != null) Destroy(v.root.gameObject);
                    _visuals.RemoveAt(i);
                }
            }

            if (!show)
            {
                for (int i = 0; i < _visuals.Count; i++)
                    if (_visuals[i].root != null && _visuals[i].root.gameObject.activeSelf)
                        _visuals[i].root.gameObject.SetActive(false);
                return;
            }

            List<Hexagon> hexes = board.HexagonsInBoard;
            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex == null || hex.currentHP <= 0 || !CarriesShield(hex)) continue;

                Visual v = Find(hex);
                if (v == null) v = Create(hex);
                if (v == null) continue;

                if (!v.root.gameObject.activeSelf) v.root.gameObject.SetActive(true);
                UpdateVisual(v, portals);
            }
        }

        /// <summary>
        /// Cette case porte-t-elle une bulle a afficher ?
        ///
        /// Deux camps, une seule grammaire : le Shofar du Yetzer Hara, et le Centre de
        /// Commandement du joueur. Le dome se lit pareil des deux cotes - tant qu'il
        /// brille, il faut le percer avant de toucher a ce qu'il y a dessous - et c'est
        /// precisement pour cela qu'on reutilise ce systeme au lieu d'en ecrire un
        /// second : le joueur passe la partie a briser la bulle d'en face, il n'a rien
        /// de nouveau a apprendre pour lire la sienne.
        /// </summary>
        private static bool CarriesShield(Hexagon hex)
        {
            if (hex == null) return false;
            if (hex.type == TypeOfHex.portal) return true;
            return hex.type == TypeOfHex.mountain && hex.level >= 1;
        }

        /// <summary>
        /// La bulle d'un Centre de Commandement : elle faiblit avec ce qu'elle a
        /// encaisse, et elle s'eteint quand elle est percee. Aucune pastille - un
        /// Centre n'a pas de fissures a compter, seulement une bulle qui tient ou non.
        /// </summary>
        private void UpdateCommandVisual(Visual v)
        {
            Hexagon hex = v.portal;
            bool broken = hex.shieldHP <= 0;

            v.broken = false;   // pas de clignotement : ce n'est pas un compte a rebours

            if (v.dome != null)
            {
                if (v.dome.enabled == broken) v.dome.enabled = !broken;

                if (!broken)
                {
                    float left = (hex.shieldMax > 0)
                                 ? Mathf.Clamp01((float)hex.shieldHP / hex.shieldMax)
                                 : 1f;

                    v.dome.GetPropertyBlock(_block);
                    _block.SetColor(RimColorId, commandShieldColor);
                    _block.SetFloat(RimStrengthId, shieldStrength * (0.3f + 0.7f * left));
                    _block.SetFloat(RimPowerId, shieldPower);
                    v.dome.SetPropertyBlock(_block);
                }
            }

            // Le disque au sol suit la bulle : il dit les cases interdites, donc il
            // disparait exactement quand elles cessent de l'etre.
            if (v.zone != null)
            {
                if (v.zone.enabled == broken) v.zone.enabled = !broken;

                if (!broken)
                {
                    float left = (hex.shieldMax > 0)
                                 ? Mathf.Clamp01((float)hex.shieldHP / hex.shieldMax)
                                 : 1f;

                    Color tint = commandZoneColor;
                    tint.a *= 0.35f + 0.65f * left;
                    SetColor(v.zone, tint);
                }
            }

            for (int p = 0; p < v.pips.Length; p++)
                if (v.pips[p] != null && v.pips[p].enabled) v.pips[p].enabled = false;
        }

        private void UpdateVisual(Visual v, PortalManager portals)
        {
            // Un Centre de Commandement porte sa bulle sur l'hexagone lui-meme, pas
            // dans PortalManager : il ne survit pas a sa destruction, donc il n'y a
            // rien a conserver apres lui.
            if (v.portal.type == TypeOfHex.mountain) { UpdateCommandVisual(v); return; }

            int needed = InteractionRules.PORTAL_KILLS_TO_BREAK_SHIELD;
            int kills = portals.GetInstability(v.portal);
            int downTurns = portals.GetShieldDownTurns(v.portal);
            bool broken = downTurns > 0;

            v.broken = broken;

            // --- la bulle : presente tant que le bouclier tient, et plus faible a
            //     chaque fissure. Rompue, elle disparait. ---
            if (v.dome != null)
            {
                if (v.dome.enabled == broken) v.dome.enabled = !broken;

                if (!broken)
                {
                    float left = 1f - Mathf.Clamp01((float)kills / Mathf.Max(1, needed));
                    v.dome.GetPropertyBlock(_block);
                    _block.SetColor(RimColorId, shieldColor);
                    _block.SetFloat(RimStrengthId, shieldStrength * (0.3f + 0.7f * left));
                    _block.SetFloat(RimPowerId, shieldPower);
                    v.dome.SetPropertyBlock(_block);
                }
            }

            // --- les pastilles : fissures (orange), ou tours restants (rouge) ---
            for (int p = 0; p < v.pips.Length; p++)
            {
                Renderer r = v.pips[p];
                if (r == null) continue;

                if (broken)
                {
                    bool lit = p < downTurns;
                    if (r.enabled != lit) r.enabled = lit;
                    // La couleur (clignotante) est posee dans LateUpdate.
                }
                else
                {
                    if (!r.enabled) r.enabled = true;
                    SetColor(r, p < kills ? pipCracked : pipEmpty);
                }
            }
        }

        /// <summary>
        /// Largeur d'une case, en unites du monde. Meme constante que BoardReadability
        /// (0.55 x racine de 3), multipliee par l'echelle reelle du plateau : une bulle
        /// mesuree en cases doit suivre le plateau s'il est redimensionne.
        /// </summary>
        private static float HexWidth()
        {
            BoardController board = BoardController.instance;
            float scale = (board != null) ? board.transform.lossyScale.x : 1f;
            if (scale < 0.00001f) scale = 1f;

            return 0.9526f * scale;
        }

        private void SetColor(Renderer r, Color c)
        {
            r.GetPropertyBlock(_block);
            _block.SetColor(ColorId, c);
            r.SetPropertyBlock(_block);
        }

        private Visual Find(Hexagon hex)
        {
            for (int i = 0; i < _visuals.Count; i++)
                if (_visuals[i].portal == hex) return _visuals[i];
            return null;
        }

        // =================================================================
        //  CONSTRUCTION (une fois par Shofar)
        // =================================================================
        private Visual Create(Hexagon portal)
        {
            // Emprise reelle du Shofar, sans ce qui est pose dessus.
            portal.GetComponentsInChildren(false, _scratch);
            bool found = false;
            Bounds bounds = new Bounds(portal.transform.position, Vector3.zero);

            for (int i = 0; i < _scratch.Count; i++)
            {
                Renderer r = _scratch[i];
                if (r == null || r is ParticleSystemRenderer || r is SpriteRenderer) continue;
                if (r.GetComponentInParent<PawnController>() != null) continue;
                if (r.GetComponent<ReadabilityDecal>() != null) continue;

                if (!found) { bounds = r.bounds; found = true; }
                else bounds.Encapsulate(r.bounds);
            }
            _scratch.Clear();

            if (!found) return null;

            Visual v = new Visual();
            v.portal = portal;

            GameObject rootGo = new GameObject(portal.type == TypeOfHex.mountain ? "Centre_Bulle" : "Shofar_Etat");
            rootGo.layer = 2;   // Ignore Raycast
            v.root = rootGo.transform;
            v.root.position = portal.transform.position;

            // --- la bulle ---
            GameObject dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "Bouclier";
            dome.layer = 2;
            Collider domeCollider = dome.GetComponent<Collider>();
            if (domeCollider != null) DestroyImmediate(domeCollider);   // jamais de clic intercepte

            Transform dt = dome.transform;
            dt.SetParent(v.root, false);
            dt.position = bounds.center;

            // LA TAILLE DIT LA REGLE.
            //
            // Sur un Shofar, la bulle habille le batiment : c'est LUI qu'elle protege,
            // et c'est lui qu'on vient frapper.
            //
            // Sur un Centre de Commandement, elle couvre des CASES - la sienne et ses
            // voisines - parce que c'est exactement ce qu'elle interdit. Un joueur qui
            // regarde le plateau doit voir ou il ne peut pas entrer, sans avoir lu une
            // seule ligne de regle. Une bulle collee au batiment aurait menti sur ce
            // qu'elle fait.
            // On lit le rayon du TYPE, pas l'etat courant : la taille du dome est figee
            // a la construction, et BubbleRadius rend zero quand la bulle est percee.
            // Creer le visuel pendant ce creux aurait donne une bulle a la taille du
            // batiment, definitivement fausse des qu'elle se refait.
            int bubbleCells = (portal.type == TypeOfHex.mountain && portal.level >= 1)
                              ? MNLTHII.Rules.InteractionRules.GetMountainRepel(portal.level)
                              : 0;

            float radius, height;

            if (bubbleCells > 0)
            {
                // Le +0.55 deborde d'un demi-hexagone : la bulle doit ENGLOBER la
                // derniere couronne interdite, pas s'arreter sur son centre.
                radius = (bubbleCells + 0.55f) * HexWidth();

                // UNE COUPOLE SURBAISSEE, ET PAS UNE SPHERE.
                //
                // Large et basse : c'est la forme qui dit "ceci couvre du terrain".
                // Une bulle haute aurait l'air d'envelopper le batiment, ce qui est
                // justement ce qu'elle ne fait pas - elle protege des CASES.
                //
                // J'avais remonte cette valeur en croyant que l'anneau se perdait dans
                // le sol. C'etait faux : il se voyait, et le demi-cercle qu'il dessinait
                // au ras du plateau etait exactement la bonne lecture. Ce qui manquait
                // n'etait pas la hauteur, c'etait la bulle elle-meme - elle n'existait
                // pas pendant les trois premieres phases (voir HexagonFactory).
                height = Mathf.Max(bounds.extents.y * shieldScale, radius * 0.45f);
            }
            else
            {
                radius = Mathf.Max(bounds.extents.x, bounds.extents.z) * shieldScale;
                height = Mathf.Max(bounds.extents.y * shieldScale, radius * 0.7f);
            }

            // LE DEFAUT QUE CE CALCUL CORRIGE.
            //
            // Le dome etait centre sur le SOL de la case, ce qui paraissait logique
            // pour une bulle qui couvre du terrain. Sauf que ce shader ne dessine que
            // le LISERE - l'anneau de silhouette, la ou la surface se detourne de la
            // camera. Centre au sol, cet anneau tombait exactement dans le plateau :
            // il etait masque par le terrain, et la bulle restait invisible.
            //
            // CENTREE SUR LE SOL DE LA CASE : la moitie basse passe sous le plateau,
            // la moitie haute dessine le demi-cercle qu'on voit. C'est la forme juste
            // pour une bulle qui couvre des cases, et elle pose son bord exactement la
            // ou l'ennemi devra s'arreter.
            if (bubbleCells > 0)
                dt.position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

            dt.localScale = new Vector3(radius * 2f, height * 2f, radius * 2f);

            // --- LE DISQUE AU SOL : les cases interdites ---
            //
            // Un lisere seul est une jolie lueur, pas une information. Ce qu'il faut
            // lire d'un coup d'oeil, c'est OU on ne peut pas entrer - et ca se dessine
            // a plat, sur les cases concernees. Le meme materiau que les pastilles de
            // fissure : un disque doux, additif, pose sur le plateau.
            if (bubbleCells > 0 && _pipMaterial != null)
            {
                GameObject zoneGo = new GameObject("Zone_Interdite");
                zoneGo.layer = 2;

                Transform zt = zoneGo.transform;
                zt.SetParent(v.root, false);

                // Trois centimetres au-dessus du plateau : assez pour ne pas clignoter
                // contre le sol, assez peu pour rester pose dessus.
                zt.position = new Vector3(bounds.center.x, bounds.min.y + 0.03f, bounds.center.z);
                zt.localScale = new Vector3(radius * 2f, radius * 2f, radius * 2f);

                zoneGo.AddComponent<MeshFilter>().sharedMesh = Quad();
                MeshRenderer zoneRenderer = zoneGo.AddComponent<MeshRenderer>();
                zoneRenderer.sharedMaterial = _pipMaterial;
                zoneRenderer.shadowCastingMode = ShadowCastingMode.Off;
                zoneRenderer.receiveShadows = false;
                v.zone = zoneRenderer;
            }

            MeshRenderer domeRenderer = dome.GetComponent<MeshRenderer>();
            domeRenderer.sharedMaterial = _domeMaterial;
            domeRenderer.shadowCastingMode = ShadowCastingMode.Off;
            domeRenderer.receiveShadows = false;
            v.dome = domeRenderer;

            // --- les pastilles, en rangee centree, face camera ---
            int count = Mathf.Max(1, InteractionRules.PORTAL_KILLS_TO_BREAK_SHIELD);
            GameObject rowGo = new GameObject("Fissures");
            rowGo.layer = 2;
            v.pipRow = rowGo.transform;
            v.pipRow.SetParent(v.root, false);
            v.pipRow.position = portal.transform.position + Vector3.up * pipHeight;

            v.pips = new Renderer[count];
            float start = -(count - 1) * pipSpacing * 0.5f;

            for (int i = 0; i < count; i++)
            {
                GameObject pip = new GameObject("Fissure" + i);
                pip.layer = 2;
                Transform pt = pip.transform;
                pt.SetParent(v.pipRow, false);
                pt.localPosition = new Vector3(start + i * pipSpacing, 0f, 0f);
                // Quad construit dans le plan XZ : on le redresse face a la camera.
                pt.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                pt.localScale = new Vector3(pipSize, pipSize, pipSize);

                pip.AddComponent<MeshFilter>().sharedMesh = Quad();
                MeshRenderer mr = pip.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _pipMaterial;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                v.pips[i] = mr;
            }

            _visuals.Add(v);
            return v;
        }

        private static Mesh Quad()
        {
            if (_quad != null) return _quad;

            _quad = new Mesh { name = "PortalPipQuad" };
            _quad.vertices = new Vector3[]
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f),
            };
            _quad.uv = new Vector2[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            _quad.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            _quad.RecalculateNormals();
            _quad.RecalculateBounds();
            return _quad;
        }

        /// <summary>Disque net au bord doux, genere une fois.</summary>
        private static Texture2D DiscTexture()
        {
            if (_discTex != null) return _discTex;

            const int size = 32;
            _discTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _discTex.name = "PortalPip";
            _discTex.wrapMode = TextureWrapMode.Clamp;

            Color32[] px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float t = Mathf.Clamp01((r - 0.78f) / 0.2f);
                    float a = 1f - t * t * (3f - 2f * t);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            _discTex.SetPixels32(px);
            _discTex.Apply(false, true);
            return _discTex;
        }
    }
}

// =============================================================================
// NOTE D'OPTIMISATION
// -----------------------------------------------------------------------------
// - Etat relu 5 fois par seconde (refreshInterval), pas a chaque frame.
// - Chaque frame : orientation de 6 rangees de pastilles et clignotement des
//   Shofars rompus. MaterialPropertyBlock reutilise, aucune allocation.
// - Un materiau partage pour toutes les bulles, un pour toutes les pastilles :
//   l'intensite et la couleur passent par le MaterialPropertyBlock.
// - Objets crees une fois par Shofar ; detruits quand le Shofar tombe.
// - Aucun collider : ni la bulle ni les pastilles n'interceptent un clic.
// =============================================================================
