using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MNLTHII.Managers
{
    /// <summary>
    /// LA FLECHE QUI DIT "CETTE CASE-LA".
    ///
    /// Deux moments ou le joueur ne savait plus de quelle case on parlait :
    ///   - apres un CLIC : le halo au sol se perd sous un Tank ou derriere un
    ///     batiment, et le menu s'ouvre a cote du curseur, pas sur la case ;
    ///   - au SURVOL D'UN CONSEIL : la camera va regarder un endroit, mais rien ne
    ///     montre lequel, surtout pour une construction (pas de fantome de trajet).
    ///
    /// Une fleche qui monte et descend au-dessus de la case regle les deux. Deux
    /// canaux : la SELECTION (suit GameManager.hexClicked toute seule, aucun
    /// branchement a faire) et le CONSEIL (AdvisorPanel l'allume au survol d'une
    /// carte et l'eteint en sortant). Chacun a sa fleche ; le conseil prend la
    /// couleur de sa carte.
    ///
    /// Construite en code : un chevron plat face camera, avec un lisere sombre
    /// derriere pour se lire sur n'importe quel fond. Le shader du projet
    /// (MNLTH_GroundDecal, toujours inclus dans le build). Aucune allocation par
    /// frame : deux maillages crees une fois, un MaterialPropertyBlock reutilise.
    /// </summary>
    public class HexPointer : MonoBehaviour
    {
        public static HexPointer Instance;

        [Header("Couleurs")]
        public Color selectionColor = new Color(1f, 0.82f, 0.36f, 1f);
        public Color outlineColor = new Color(0.04f, 0.05f, 0.10f, 0.85f);

        [Header("Forme et mouvement")]
        [Tooltip("Largeur de la fleche, en unites monde (une case fait ~0.95).")]
        public float width = 0.46f;
        [Tooltip("Hauteur totale de la fleche.")]
        public float height = 0.62f;
        [Tooltip("Marge entre le sommet de ce qui occupe la case et la pointe de la fleche.")]
        public float clearance = 0.22f;
        [Tooltip("Amplitude du va-et-vient vertical.")]
        public float bounce = 0.14f;
        public float bounceSpeed = 5.5f;

        private sealed class Arrow
        {
            public Transform root;
            public Renderer fill;
            public Renderer outline;
            public Hexagon hex;
            public Color color;
            public float phase;
        }

        private Arrow _selection;
        private Arrow _hint;
        private Material _material;
        private MaterialPropertyBlock _block;
        private Mesh _mesh;
        private Transform _camTransform;

        private static readonly List<Renderer> _measure = new List<Renderer>();
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");

        // =================================================================
        //  CYCLE DE VIE (meme recette que PortalStatusVisual : une par scene)
        // =================================================================
        private static bool _sceneHooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!_sceneHooked)
            {
                _sceneHooked = true;
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            }

            if (Instance != null) return;
            new GameObject("HexPointer (auto)").AddComponent<HexPointer>();
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                                          UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            Bootstrap();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            _block = new MaterialPropertyBlock();

            Shader shader = Resources.Load<Shader>("Shaders/MNLTH_GroundDecal");
            if (shader == null)
            {
                Debug.LogWarning("[HexPointer] Shader MNLTH_GroundDecal introuvable : pas de fleche de selection.");
                return;
            }

            _material = new Material(shader) { name = "Fleche de case", enableInstancing = true };
            _material.SetTexture(MainTexId, Texture2D.whiteTexture);
            _material.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
            _material.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
            _material.SetColor(ColorId, Color.white);
            // Au-dessus des pastilles et des decals : la fleche ne se cache jamais.
            _material.renderQueue = 3150;

            _mesh = BuildArrow();
            _selection = Build("Selection");
            _hint = Build("Conseil");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_material != null) Destroy(_material);
            if (_mesh != null) Destroy(_mesh);
        }

        // =================================================================
        //  API
        // =================================================================
        /// <summary>Le conseiller montre cette case (survol d'une carte).</summary>
        public static void ShowHint(Hexagon hex, Color color)
        {
            if (Instance == null || Instance._hint == null) return;
            Instance.Point(Instance._hint, hex, color);
        }

        public static void HideHint()
        {
            if (Instance == null || Instance._hint == null) return;
            Instance.Point(Instance._hint, null, Color.white);
        }

        // =================================================================
        //  BOUCLE
        // =================================================================
        private void LateUpdate()
        {
            if (_selection == null) return;

            if (_camTransform == null)
            {
                Camera cam = Camera.main;
                if (cam != null) _camTransform = cam.transform;
            }

            // LA SELECTION SUIT LE CLIC : la case cliquee, tant que le jeu la garde
            // (GameManager.hexClicked, efface a la fin de la phase). Pendant un plan
            // immersif, on regarde : la fleche s'efface.
            Hexagon selected = null;
            GameManager gm = GameManager.instance;
            if (gm != null && gm.hexClicked != null && !ImmersiveCamera.BlocksBoardInput)
            {
                TurnManager turns = TurnManager.Instance;
                if (turns == null || !turns.gameOver) selected = gm.hexClicked;
            }

            if (_selection.hex != selected) Point(_selection, selected, selectionColor);

            Animate(_selection);
            Animate(_hint);
        }

        private void Point(Arrow a, Hexagon hex, Color color)
        {
            if (a == null || a.root == null) return;

            bool changed = (a.hex != hex);
            a.hex = hex;
            a.color = color;

            if (hex == null)
            {
                if (a.root.gameObject.activeSelf) a.root.gameObject.SetActive(false);
                return;
            }

            if (changed) a.phase = Time.unscaledTime;   // la fleche repart du haut

            SetColor(a.fill, color);
            SetColor(a.outline, outlineColor);
            if (!a.root.gameObject.activeSelf) a.root.gameObject.SetActive(true);
        }

        private void Animate(Arrow a)
        {
            if (a == null || a.root == null || !a.root.gameObject.activeSelf) return;

            // La case a disparu (batiment detruit, scene dechargee) : on eteint.
            if (a.hex == null)
            {
                a.hex = null;
                a.root.gameObject.SetActive(false);
                return;
            }

            float top = TopOf(a.hex);
            float t = (Time.unscaledTime - a.phase) * bounceSpeed;
            float dip = 0.5f + 0.5f * Mathf.Cos(t);          // 1 en haut, 0 en bas, part du haut

            Vector3 p = a.hex.transform.position;
            p.y = top + clearance + bounce * dip;
            a.root.position = p;

            if (_camTransform != null) a.root.rotation = _camTransform.rotation;
        }

        /// <summary>
        /// Le sommet de ce qui occupe la case : le terrain, le batiment ET le pion
        /// pose dessus. La fleche doit flotter au-dessus de tout, jamais dedans.
        /// </summary>
        private static float TopOf(Hexagon hex)
        {
            Transform t = hex.transform;
            float top = t.position.y;

            t.GetComponentsInChildren(false, _measure);
            for (int i = 0; i < _measure.Count; i++)
            {
                Renderer r = _measure[i];
                if (r == null || !r.enabled) continue;
                if (r is ParticleSystemRenderer || r is SpriteRenderer || r is LineRenderer) continue;
                if (r.GetComponent<ReadabilityDecal>() != null) continue;
                float y = r.bounds.max.y;
                if (y > top) top = y;
            }
            _measure.Clear();

            // Un pion pose sur la case mais range ailleurs dans la hierarchie.
            BoardController board = BoardController.instance;
            if (board != null && board.PawnsInBoard != null)
            {
                for (int i = 0; i < board.PawnsInBoard.Count; i++)
                {
                    PawnController pawn = board.PawnsInBoard[i];
                    if (pawn == null || pawn.hexcoord == null || hex.positionInTheBoard == null) continue;
                    if (!pawn.hexcoord.CompareHexCoord(hex.positionInTheBoard)) continue;

                    pawn.GetComponentsInChildren(false, _measure);
                    for (int k = 0; k < _measure.Count; k++)
                    {
                        Renderer r = _measure[k];
                        if (r == null || !r.enabled) continue;
                        if (r is ParticleSystemRenderer || r is SpriteRenderer || r is LineRenderer) continue;
                        if (r.GetComponent<ReadabilityDecal>() != null) continue;
                        float y = r.bounds.max.y;
                        if (y > top) top = y;
                    }
                    _measure.Clear();
                }
            }

            return top;
        }

        private void SetColor(Renderer r, Color c)
        {
            if (r == null) return;
            r.GetPropertyBlock(_block);
            _block.SetColor(ColorId, c);
            r.SetPropertyBlock(_block);
        }

        // =================================================================
        //  CONSTRUCTION
        // =================================================================
        private Arrow Build(string name)
        {
            Arrow a = new Arrow();

            GameObject rootGo = new GameObject("Fleche_" + name);
            rootGo.layer = 2;   // Ignore Raycast : la fleche n'intercepte aucun clic
            rootGo.transform.SetParent(transform, false);
            a.root = rootGo.transform;

            // Le lisere sombre, un peu plus grand, legerement derriere.
            a.outline = MakePart(a.root, "Lisere", 1.22f, 0.012f);
            a.fill = MakePart(a.root, "Fleche", 1f, 0f);

            rootGo.SetActive(false);
            return a;
        }

        private Renderer MakePart(Transform parent, string name, float scale, float back)
        {
            GameObject go = new GameObject(name);
            go.layer = 2;
            Transform t = go.transform;
            t.SetParent(parent, false);
            // Le maillage pointe vers le bas depuis l'origine ; le lisere est centre
            // sur la fleche en etant un peu plus grand.
            t.localPosition = new Vector3(0f, -height * (scale - 1f) * 0.5f, back);
            t.localScale = new Vector3(scale, scale, 1f);

            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return mr;
        }

        /// <summary>
        /// Un chevron plat dans le plan XY, pointe en (0,0), qui monte vers +Y : une
        /// tete triangulaire et un fut. Les deux faces sont dessinees (pas de
        /// culling a l'envers quand la camera tourne), d'ou les triangles doubles.
        /// </summary>
        private Mesh BuildArrow()
        {
            float w = width * 0.5f;        // demi-largeur de la tete
            float sw = width * 0.19f;      // demi-largeur du fut
            float headH = height * 0.48f;  // hauteur de la tete
            float h = height;

            Vector3[] v =
            {
                new Vector3(0f, 0f, 0f),          // 0 pointe
                new Vector3(-w, headH, 0f),       // 1 tete gauche
                new Vector3(w, headH, 0f),        // 2 tete droite
                new Vector3(-sw, headH, 0f),      // 3 fut bas gauche
                new Vector3(sw, headH, 0f),       // 4 fut bas droite
                new Vector3(-sw, h, 0f),          // 5 fut haut gauche
                new Vector3(sw, h, 0f),           // 6 fut haut droite
            };

            int[] tri =
            {
                0, 1, 2,   0, 2, 1,               // tete, deux faces
                3, 5, 6,   3, 6, 4,               // fut
                3, 6, 5,   3, 4, 6,
            };

            Vector2[] uv = new Vector2[v.Length];
            for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(0.5f, 0.5f);

            Mesh m = new Mesh { name = "Fleche de case" };
            m.vertices = v;
            m.uv = uv;
            m.triangles = tri;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
