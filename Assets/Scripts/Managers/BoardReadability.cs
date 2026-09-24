using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using MNLTHII;

namespace MNLTHII.Managers
{
    /// <summary>
    /// LISIBILITE DU PLATEAU : faire ressortir les pions.
    ///
    /// Le diagnostic (capture passee en noir et blanc) : les pions disparaissaient.
    /// Le decor etait aussi sature, aussi contraste et parfois plus haut qu'eux, et les
    /// couleurs d'equipe etaient deja prises par le terrain - Tanks bleus sur cristaux
    /// bleus, ennemis violets sur gaz violet.
    ///
    /// Six leviers, tous bon marche en WebGL (aucun post-effet, aucune passe plein ecran) :
    ///
    ///   1. PLATEAU CALME     les cases de terrain perdent une partie de leur couleur et
    ///                        de leur contraste. Elles restent reconnaissables, mais
    ///                        elles cessent de crier.
    ///   2. MONTAGNES BASSES  le relief est ecrase autour de la surface du plateau : il
    ///                        ne depasse plus les pions.
    ///   3. LISERE            un bord lumineux a la couleur de l'equipe suit la
    ///                        silhouette des pions et des batiments.
    ///   4. OMBRE DE CONTACT  un disque sombre sous chaque pion le "pose" au sol.
    ///   5. ANNEAU DE SOCLE   cyan sous les tiens, rouge sous les ennemis.
    ///   6. TAILLE            pions un peu plus grands, ennemis un peu plus encore.
    ///
    /// RIEN N'EST MODIFIE SUR LE DISQUE. Les materiaux d'origine restent intacts : on en
    /// fabrique des copies au lancement. Decoche "active" et le jeu redevient exactement
    /// ce qu'il etait.
    ///
    /// REGLAGES : les curseurs (couleurs, intensites, desaturation...) se reglent en
    /// direct pendant la partie. Les cases a cocher et les tailles ne s'appliquent
    /// qu'aux pions et cases crees ensuite - relance la partie pour les juger.
    ///
    /// Si aucun objet de la scene ne porte ce composant, il se cree tout seul avec les
    /// reglages par defaut. Pour regler : ajoute-le sur l'objet des gestionnaires.
    /// </summary>
    public class BoardReadability : MonoBehaviour
    {
        public static BoardReadability Instance;

        [System.Serializable]
        public class TerrainTuning
        {
            public TypeOfHex type = TypeOfHex.mountain;

            [Tooltip("Multiplie la luminosite de ce terrain (1 = inchange).")]
            [Range(0.3f, 2f)] public float brightness = 1f;

            [Tooltip("Hauteur du relief au-dessus de la surface (1 = inchange, 0.7 = 30% plus bas).")]
            [Range(0.3f, 1f)] public float heightScale = 1f;
        }

        // =================================================================
        //  REGLAGES
        // =================================================================
        [Header("Interrupteur general")]
        [Tooltip("Decoche : tout redevient comme avant (au prochain lancement).")]
        public bool active = true;

        [Header("1. Plateau calme")]
        public bool calmBoard = true;

        [Tooltip("0 = couleurs d'origine, 1 = noir et blanc.")]
        [Range(0f, 1f)] public float desaturation = 0.45f;

        [Tooltip("1 = contraste d'origine. Plus bas : les cases claires et sombres se rapprochent.")]
        [Range(0.2f, 1f)] public float contrast = 0.7f;

        [Tooltip("La clarte vers laquelle le contraste se resserre.")]
        [Range(0f, 1f)] public float contrastPivot = 0.38f;

        [Range(0.5f, 1.5f)] public float brightness = 1f;

        [Tooltip("Desaturation des parties lumineuses du decor (cristaux, flammes).")]
        [Range(0f, 1f)] public float emissionDesaturation = 0.6f;

        [Tooltip("Intensite des parties lumineuses du decor (1 = inchange).")]
        [Range(0f, 1f)] public float emissionScale = 0.55f;

        [Tooltip("Reglages par type de terrain. Par defaut : montagnes eclaircies et abaissees.")]
        public TerrainTuning[] terrainTuning = new TerrainTuning[]
        {
            new TerrainTuning { type = TypeOfHex.mountain, brightness = 1.35f, heightScale = 0.7f },
        };

        // =================================================================
        //  1 bis. LA COULEUR DES PLAINES
        // =================================================================
        /// <summary>
        /// LA PLAINE TIRE VERS LA TERRE CUITE, LA COLLINE RESTE VERTE.
        ///
        /// La plaine est le seul terrain qui porte un Tank, et depuis que tout
        /// l'interieur du plateau en est devenu une, c'est l'information de sol la plus
        /// consultee de la partie. Elle etait pourtant du meme vert eteint que la
        /// colline : deux terrains qui se jouent de facons opposees - l'un se batit, on
        /// ne marche pas sur l'autre - et qui se ressemblaient de loin.
        ///
        /// Le calme du plateau aggravait le probleme : en desaturant, il rapprochait
        /// justement les verts.
        ///
        /// Une teinte chaude separe les deux sans crier. Ce n'est pas un rouge vif -
        /// la montagne est deja rouge, et le plateau doit rester en retrait derriere
        /// les pions. C'est une terre seche : verte contre ocre, la difference se lit
        /// d'un coup d'oeil meme en pleine desaturation.
        ///
        /// Elle MULTIPLIE l'albedo au lieu de le remplacer : le relief, les ombres et
        /// les details du modele restent visibles, la case est seulement rechauffee.
        /// </summary>
        [Header("1 bis. Couleur des plaines")]
        [Tooltip("Decoche : les plaines gardent la couleur de leur modele.")]
        public bool tintPlains = true;

        /// <summary>
        /// POUSSEE PLUS LOIN QU'ON NE LA CHOISIRAIT A L'OEIL NU, et c'est voulu.
        ///
        /// Le shader calme desature de 45 % APRES avoir multiplie par cette couleur :
        /// pres de la moitie de la teinte est donc mangee avant d'arriver a l'ecran.
        /// Mesure sur des verts de terrain representatifs, ecart colline / plaine une
        /// fois le plateau calme :
        ///
        ///   sans teinte            0,07   les deux se confondent
        ///   (1 / 0,70 / 0,60)      0,17
        ///   (1 / 0,55 / 0,42)      0,27   lisible sans crier
        ///
        /// Si elle te parait trop forte dans l'editeur AVANT de lancer, c'est normal -
        /// regarde-la en jeu.
        /// </summary>
        [Tooltip("Multiplie la couleur des plaines. Blanc = inchange.")]
        public Color plainTint = new Color(1f, 0.55f, 0.42f, 1f);

        [Header("2. Couleurs d'equipe")]
        public Color playerColor = new Color(0.25f, 0.85f, 1f, 1f);
        public Color enemyColor = new Color(1f, 0.32f, 0.2f, 1f);

        [Header("3. Lisere lumineux")]
        public bool rimOnPawns = true;
        public bool rimOnBuildings = true;

        [Tooltip("Plus haut : lisere plus fin, colle au bord.")]
        [Range(0.5f, 8f)] public float rimPower = 2.5f;

        [Range(0f, 4f)] public float rimStrength = 1.3f;

        [Tooltip("Les batiments sont grands : un lisere plus discret suffit.")]
        [Range(0f, 4f)] public float buildingRimStrength = 0.7f;

        [Header("4. Ombre de contact")]
        public bool contactShadow = true;
        [Range(0f, 1f)] public float shadowOpacity = 0.3f;

        [Tooltip("Taille de l'ombre par rapport a l'emprise du pion.")]
        [Range(0.5f, 2.5f)] public float shadowSize = 1.25f;

        [Tooltip("Les ennemis volent : leur ombre est plus legere.")]
        [Range(0f, 1f)] public float flyingShadowOpacity = 0.18f;

        [Header("5. Anneau de socle")]
        public bool teamRing = true;

        [Tooltip("Diametre, en fraction de la largeur d'une case.")]
        [Range(0.3f, 1.2f)] public float ringSize = 0.8f;

        [Range(0f, 2f)] public float ringIntensity = 0.9f;

        [Header("6. Taille des pions")]
        [Range(0.5f, 2f)] public float playerPawnScale = 1.2f;
        [Range(0.5f, 2f)] public float enemyPawnScale = 1.35f;

        [Header("Avance")]
        [Tooltip("Hauteur monde de la surface des cases. 0 = mesuree automatiquement.")]
        public float surfaceHeightOverride = 0f;

        // =================================================================
        //  CONSTANTES
        // =================================================================
        private const string CalmShaderPath = "Shaders/MNLTH_BoardCalm";
        private const string RimShaderPath = "Shaders/MNLTH_PawnRim";
        private const string DecalShaderPath = "Shaders/MNLTH_GroundDecal";
        private const string RimShaderName = "MNLTH/PawnRim";

        /// <summary>Distance centre a centre de deux cases voisines, en unites du plateau (0.55 * racine de 3).</summary>
        private const float HexWidthBoardUnits = 0.9526f;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int UseMetallicMapId = Shader.PropertyToID("_UseMetallicMap");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int RimPowerId = Shader.PropertyToID("_RimPower");
        private static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");
        private static readonly int CalmDesaturateId = Shader.PropertyToID("_CalmDesaturate");
        private static readonly int CalmContrastId = Shader.PropertyToID("_CalmContrast");
        private static readonly int CalmPivotId = Shader.PropertyToID("_CalmPivot");
        private static readonly int CalmBrightnessId = Shader.PropertyToID("_CalmBrightness");
        private static readonly int CalmEmissionDesaturateId = Shader.PropertyToID("_CalmEmissionDesaturate");
        private static readonly int CalmEmissionScaleId = Shader.PropertyToID("_CalmEmissionScale");

        // =================================================================
        //  RESSOURCES PARTAGEES
        // =================================================================
        private Shader _calmShader;
        private Shader _rimShader;
        private Shader _decalShader;

        private Material _rimPlayer;
        private Material _rimEnemy;
        private Material _rimPlayerBuilding;
        private Material _rimEnemyBuilding;
        private Material _shadowMat;
        private Material _shadowFlyingMat;
        private Material _ringPlayer;
        private Material _ringEnemy;

        // Une copie "calme" par couple (materiau d'origine, type de terrain) : deux
        // terrains qui partagent un materiau peuvent ainsi avoir des reglages differents.
        private Dictionary<Material, Material>[] _calmCache;
        private readonly List<Material> _calmMaterials = new List<Material>();
        private readonly List<float> _calmTypeBrightness = new List<float>();

        // La couleur d'origine de chaque copie calme, et la teinte a lui appliquer.
        // Deux listes paralleles plutot qu'une structure : ApplyCalmParams est rejouee
        // a chaud quand un reglage change, et multiplier la couleur en place la ferait
        // foncer un peu plus a chaque passage.
        private readonly List<Color> _calmBaseColor = new List<Color>();
        private readonly List<Color> _calmTint = new List<Color>();

        private static Mesh _quad;
        private static Texture2D _shadowTex;
        private static Texture2D _ringTex;

        private bool _surfaceKnown;
        private float _surfaceY;

        // Tampons reutilises : GetComponentsInChildren(List) n'alloue pas.
        private readonly List<Renderer> _renderers = new List<Renderer>(16);
        private readonly List<Renderer> _surfaceScratch = new List<Renderer>(8);
        private readonly List<MeshRenderer> _meshScratch = new List<MeshRenderer>(8);
        private readonly List<SkinnedMeshRenderer> _skinnedScratch = new List<SkinnedMeshRenderer>(4);

        // =================================================================
        //  CYCLE DE VIE
        // =================================================================
        /// <summary>
        /// Filet de securite : si personne n'a pose le composant dans la scene, il se
        /// cree tout seul. Appele apres les Awake de la scene mais AVANT les Start, donc
        /// avant que la moindre case ou le moindre pion ne demande a etre decore.
        /// </summary>
        // Le composant vit dans la scene chargee : il disparait avec elle. Sans ce
        // rappel, il n'etait cree que pour la PREMIERE scene - depuis le menu
        // principal (ou apres "Rejouer"), la scene de jeu n'en avait plus, et le
        // plateau reprenait ses anciennes couleurs.
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

            GameObject go = new GameObject("BoardReadability (auto)");
            go.AddComponent<BoardReadability>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;

            int typeCount = System.Enum.GetValues(typeof(TypeOfHex)).Length;
            _calmCache = new Dictionary<Material, Material>[typeCount];
            for (int i = 0; i < typeCount; i++) _calmCache[i] = new Dictionary<Material, Material>(8);

            _calmShader = Resources.Load<Shader>(CalmShaderPath);
            _rimShader = Resources.Load<Shader>(RimShaderPath);
            _decalShader = Resources.Load<Shader>(DecalShaderPath);

            if (_calmShader == null || _rimShader == null || _decalShader == null)
            {
                Debug.LogWarningFormat("[BoardReadability] Shader(s) introuvable(s) dans Resources/Shaders " +
                                       "(calme: {0}, lisere: {1}, sol: {2}). Les effets concernes sont desactives.",
                                       _calmShader != null, _rimShader != null, _decalShader != null);
            }

            BuildSharedMaterials();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            DestroyMaterial(_rimPlayer);
            DestroyMaterial(_rimEnemy);
            DestroyMaterial(_rimPlayerBuilding);
            DestroyMaterial(_rimEnemyBuilding);
            DestroyMaterial(_shadowMat);
            DestroyMaterial(_shadowFlyingMat);
            DestroyMaterial(_ringPlayer);
            DestroyMaterial(_ringEnemy);

            for (int i = 0; i < _calmMaterials.Count; i++) DestroyMaterial(_calmMaterials[i]);
            _calmMaterials.Clear();
            _calmTypeBrightness.Clear();
            _calmBaseColor.Clear();
            _calmTint.Clear();
        }

        private static void DestroyMaterial(Material m)
        {
            if (m != null) Destroy(m);
        }

#if UNITY_EDITOR
        /// <summary>Reglage en direct : bouger un curseur en jeu repeint tout de suite.</summary>
        private void OnValidate()
        {
            if (!Application.isPlaying || Instance != this) return;
            RefreshMaterials();
        }
#endif

        // =================================================================
        //  POINTS D'ENTREE (appeles par Hexagon, PawnController, ThreatPreview)
        // =================================================================
        /// <summary>Une case vient d'etre creee : on la calme, ou on lui donne son lisere.</summary>
        public static void NotifyHexReady(Hexagon hex)
        {
            BoardReadability self = Instance;
            if (self == null || !self.active || hex == null) return;
            self.ApplyToHex(hex);
        }

        /// <summary>Un pion vient d'apparaitre : ombre, anneau, lisere, taille.</summary>
        public static void NotifyPawnReady(PawnController pawn)
        {
            BoardReadability self = Instance;
            if (self == null || !self.active || pawn == null) return;
            self.DecoratePawn(pawn);
        }

        /// <summary>
        /// Retire d'un clone tout ce que ce composant a ajoute. ThreatPreview l'appelle
        /// avant d'en faire un fantome : un fantome n'a ni ombre, ni anneau, ni lisere.
        /// </summary>
        public static void StripDecorations(GameObject root)
        {
            if (root == null) return;

            ReadabilityDecal[] decals = root.GetComponentsInChildren<ReadabilityDecal>(true);
            for (int i = 0; i < decals.Length; i++)
                if (decals[i] != null) DestroyImmediate(decals[i].gameObject);

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;

                Material[] mats = r.sharedMaterials;
                int keep = 0;
                for (int m = 0; m < mats.Length; m++) if (!IsRimMaterial(mats[m])) keep++;
                if (keep == mats.Length || keep == 0) continue;

                Material[] kept = new Material[keep];
                int k = 0;
                for (int m = 0; m < mats.Length; m++) if (!IsRimMaterial(mats[m])) kept[k++] = mats[m];
                r.sharedMaterials = kept;
            }
        }

        private static bool IsRimMaterial(Material m)
        {
            return m != null && m.shader != null && m.shader.name == RimShaderName;
        }

        // =================================================================
        //  CASES
        // =================================================================
        private void ApplyToHex(Hexagon hex)
        {
            CollectRenderers(hex.transform, false);
            if (_renderers.Count == 0) return;

            bool portal = hex.type == TypeOfHex.portal;
            bool destroyed = hex.type == TypeOfHex.Destroyed;
            bool building = !portal && !destroyed && (hex.type == TypeOfHex.Base || hex.level > 0);

            // Portails et batiments : ils appartiennent a un camp, ils prennent son lisere
            // et gardent leurs couleurs. Ce sont des pieces du jeu, pas du decor.
            if (portal || building)
            {
                if (rimOnBuildings) AddRim(portal ? _rimEnemyBuilding : _rimPlayerBuilding);
                return;
            }

            // Terrain (et ruines) : on le calme.
            TerrainTuning tuning = FindTuning(hex.type);
            int typeIndex = (int)hex.type;

            if (calmBoard && _calmShader != null && typeIndex >= 0 && typeIndex < _calmCache.Length)
            {
                float typeBrightness = (tuning != null) ? tuning.brightness : 1f;
                Color typeTint = TintFor(hex.type);

                for (int i = 0; i < _renderers.Count; i++)
                    SwapToCalm(_renderers[i], typeIndex, typeBrightness, typeTint);
            }

            if (tuning != null && tuning.heightScale < 0.999f)
                Flatten(hex.transform, tuning.heightScale);
        }

        /// <summary>
        /// La teinte a appliquer a ce terrain. Blanc partout sauf sur la plaine : c'est
        /// le seul terrain dont la lecture change une decision, donc le seul qui merite
        /// de se distinguer de son voisin.
        /// </summary>
        private Color TintFor(TypeOfHex type)
        {
            if (tintPlains && type == TypeOfHex.plain) return plainTint;
            return Color.white;
        }

        private TerrainTuning FindTuning(TypeOfHex type)
        {
            if (terrainTuning == null) return null;

            for (int i = 0; i < terrainTuning.Length; i++)
                if (terrainTuning[i] != null && terrainTuning[i].type == type) return terrainTuning[i];

            return null;
        }

        private void SwapToCalm(Renderer r, int typeIndex, float typeBrightness, Color typeTint)
        {
            if (r == null) return;

            Material[] mats = r.sharedMaterials;
            bool changed = false;

            for (int m = 0; m < mats.Length; m++)
            {
                Material calm = GetCalm(mats[m], typeIndex, typeBrightness, typeTint);
                if (calm == null) continue;

                mats[m] = calm;
                changed = true;
            }

            if (changed) r.sharedMaterials = mats;
        }

        /// <summary>
        /// La copie calme d'un materiau Standard opaque. Tout autre materiau (particules,
        /// transparents, shaders maison) est laisse tel quel : on ne sait pas le refaire
        /// fidelement, donc on n'y touche pas.
        /// </summary>
        private Material GetCalm(Material src, int typeIndex, float typeBrightness, Color typeTint)
        {
            if (src == null || src.shader == null) return null;
            if (src.shader.name != "Standard") return null;
            if (src.HasProperty(ModeId) && src.GetFloat(ModeId) > 0.5f) return null;

            Dictionary<Material, Material> cache = _calmCache[typeIndex];

            Material calm;
            if (cache.TryGetValue(src, out calm)) return calm;

            calm = new Material(_calmShader);
            calm.name = src.name + " (calme)";
            calm.CopyPropertiesFromMaterial(src);

            // Le Standard active ses options par mots-cles ; le shader calme par des
            // valeurs. On traduit, pour qu'une carte absente ne soit jamais lue.
            calm.SetFloat(UseMetallicMapId, src.IsKeywordEnabled("_METALLICGLOSSMAP") ? 1f : 0f);
            if (!src.IsKeywordEnabled("_NORMALMAP")) calm.SetTexture(BumpMapId, null);
            if (!src.IsKeywordEnabled("_EMISSION")) calm.SetColor(EmissionColorId, Color.black);

            // Le shader calme n'a pas de variante instanciee : on ne la demande pas.
            calm.enableInstancing = false;

            // La couleur d'origine est relevee AVANT toute teinte : c'est elle qu'on
            // remultipliera a chaque reglage rejoue a chaud.
            Color baseColor = src.HasProperty(ColorId) ? src.GetColor(ColorId) : Color.white;

            ApplyCalmParams(calm, typeBrightness, baseColor, typeTint);

            cache.Add(src, calm);
            _calmMaterials.Add(calm);
            _calmTypeBrightness.Add(typeBrightness);
            _calmBaseColor.Add(baseColor);
            _calmTint.Add(typeTint);
            return calm;
        }

        private void ApplyCalmParams(Material calm, float typeBrightness, Color baseColor, Color tint)
        {
            // La teinte MULTIPLIE : le relief et les details du modele restent lisibles,
            // la case est seulement rechauffee. L'alpha d'origine est preserve - un
            // terrain opaque doit le rester.
            Color tinted = new Color(baseColor.r * tint.r, baseColor.g * tint.g,
                                     baseColor.b * tint.b, baseColor.a);
            calm.SetColor(ColorId, tinted);

            calm.SetFloat(CalmDesaturateId, desaturation);
            calm.SetFloat(CalmContrastId, contrast);
            calm.SetFloat(CalmPivotId, contrastPivot);
            calm.SetFloat(CalmBrightnessId, brightness * typeBrightness);
            calm.SetFloat(CalmEmissionDesaturateId, emissionDesaturation);
            calm.SetFloat(CalmEmissionScaleId, emissionScale);
        }

        /// <summary>
        /// Ecrase le relief autour de la surface du plateau. Le dessus de la case reste a
        /// sa hauteur - un pion qui s'y pose ne flotte pas et ne s'enfonce pas - seul ce
        /// qui depasse est abaisse.
        /// </summary>
        private void Flatten(Transform hexRoot, float heightScale)
        {
            float surface = GetSurfaceY(float.NaN);
            if (float.IsNaN(surface)) return;

            Vector3 rootPos = hexRoot.position;
            float pivot = hexRoot.InverseTransformPoint(new Vector3(rootPos.x, surface, rootPos.z)).y;

            int count = hexRoot.childCount;
            for (int i = 0; i < count; i++)
            {
                Transform child = hexRoot.GetChild(i);
                if (!IsModelChild(child)) continue;

                if (!ScaleAlongParentUp(child, heightScale)) continue;

                Vector3 p = child.localPosition;
                p.y = pivot + (p.y - pivot) * heightScale;
                child.localPosition = p;
            }
        }

        /// <summary>
        /// Ecrase un enfant selon l'axe VERTICAL de son parent. Les modeles importes sont
        /// souvent tournes (-90 en X) : leur Y local n'est alors plus la verticale. On
        /// cherche donc lequel de leurs axes est vertical, et on n'agit que s'il y en a
        /// un franchement - sinon on s'abstient plutot que de deformer le modele.
        /// </summary>
        private static bool ScaleAlongParentUp(Transform child, float s)
        {
            Quaternion q = child.localRotation;
            float dx = Mathf.Abs((q * Vector3.right).y);
            float dy = Mathf.Abs((q * Vector3.up).y);
            float dz = Mathf.Abs((q * Vector3.forward).y);

            Vector3 ls = child.localScale;

            if (dy >= dx && dy >= dz && dy > 0.999f) ls.y *= s;
            else if (dx >= dz && dx > 0.999f) ls.x *= s;
            else if (dz > 0.999f) ls.z *= s;
            else return false;

            child.localScale = ls;
            return true;
        }

        // =================================================================
        //  PIONS
        // =================================================================
        private void DecoratePawn(PawnController pawn)
        {
            Transform root = pawn.transform;
            bool enemy = pawn.IsEnemy;

            CollectRenderers(root, true);
            if (_renderers.Count == 0) return;

            Bounds bounds = _renderers[0].bounds;
            for (int i = 1; i < _renderers.Count; i++) bounds.Encapsulate(_renderers[i].bounds);

            float surface = GetSurfaceY(bounds.min.y);

            // Un ennemi vole : son bas est nettement au-dessus du sol.
            bool flying = (bounds.min.y - surface) > Mathf.Max(0.02f, bounds.size.y * 0.2f);

            // 6. Taille. On agrandit autour du SOL pour un Tank (il reste pose), autour
            //    du CORPS pour un ennemi volant (il garde son altitude).
            float scale = enemy ? enemyPawnScale : playerPawnScale;
            if (Mathf.Abs(scale - 1f) > 0.001f)
            {
                Vector3 rootPos = root.position;
                float pivotWorld = flying ? bounds.center.y : surface;
                float pivotLocal = root.InverseTransformPoint(new Vector3(rootPos.x, pivotWorld, rootPos.z)).y;
                ScaleChildrenUniform(root, pivotLocal, scale);
            }

            float footprint = Mathf.Max(bounds.extents.x, bounds.extents.z) * scale;
            float lift = 0.004f * BoardScale();

            // 4. Ombre de contact.
            if (contactShadow && _decalShader != null)
            {
                Material shadow = flying ? _shadowFlyingMat : _shadowMat;
                CreateDecal(root, shadow, surface + lift, footprint * 2f * shadowSize, "ReadabilityShadow");
            }

            // 5. Anneau de socle, pose un cran au-dessus de l'ombre.
            if (teamRing && _decalShader != null)
            {
                Material ring = enemy ? _ringEnemy : _ringPlayer;
                CreateDecal(root, ring, surface + lift * 2f, HexWidth() * ringSize, "ReadabilityRing");
            }

            // 3. Lisere.
            if (rimOnPawns) AddRim(enemy ? _rimEnemy : _rimPlayer);
        }

        private static void ScaleChildrenUniform(Transform root, float pivotLocalY, float s)
        {
            int count = root.childCount;
            for (int i = 0; i < count; i++)
            {
                Transform child = root.GetChild(i);
                if (!IsModelChild(child)) continue;

                Vector3 p = child.localPosition;
                child.localPosition = new Vector3(p.x * s, pivotLocalY + (p.y - pivotLocalY) * s, p.z * s);
                child.localScale = child.localScale * s;
            }
        }

        /// <summary>
        /// Un enfant qui fait partie du MODELE : actif, et ni un disque ajoute par nous,
        /// ni un pion pose sur la case (les pions sont enfants de leur case).
        /// </summary>
        private static bool IsModelChild(Transform child)
        {
            if (child == null || !child.gameObject.activeSelf) return false;
            if (child.GetComponent<ReadabilityDecal>() != null) return false;
            if (child.GetComponent<PawnController>() != null) return false;
            return true;
        }

        // =================================================================
        //  OUTILS
        // =================================================================
        /// <summary>
        /// Les renderers actifs qui appartiennent a CET objet : ni ceux d'un pion pose
        /// dessus, ni nos disques, ni les particules ou barres de vie.
        /// </summary>
        private void CollectRenderers(Transform root, bool includeSkinned)
        {
            CollectRenderers(root, includeSkinned, _renderers);
        }

        private void CollectRenderers(Transform root, bool includeSkinned, List<Renderer> into)
        {
            into.Clear();

            root.GetComponentsInChildren(false, _meshScratch);
            for (int i = 0; i < _meshScratch.Count; i++)
            {
                MeshRenderer r = _meshScratch[i];
                if (r != null && BelongsTo(r.transform, root)) into.Add(r);
            }

            if (!includeSkinned) return;

            root.GetComponentsInChildren(false, _skinnedScratch);
            for (int i = 0; i < _skinnedScratch.Count; i++)
            {
                SkinnedMeshRenderer r = _skinnedScratch[i];
                if (r != null && BelongsTo(r.transform, root)) into.Add(r);
            }
        }

        private static bool BelongsTo(Transform t, Transform root)
        {
            while (t != null && t != root)
            {
                if (t.GetComponent<ReadabilityDecal>() != null) return false;
                if (t.GetComponent<PawnController>() != null) return false;
                t = t.parent;
            }
            return true;
        }

        private void AddRim(Material rim)
        {
            if (rim == null) return;

            for (int i = 0; i < _renderers.Count; i++)
            {
                Renderer r = _renderers[i];
                if (r == null) continue;

                Material[] mats = r.sharedMaterials;

                bool already = false;
                for (int m = 0; m < mats.Length; m++) if (mats[m] == rim) { already = true; break; }
                if (already) continue;

                // Un materiau de plus que de sous-maillages : Unity redessine le dernier
                // sous-maillage avec ce materiau. C'est exactement la passe de lisere.
                Material[] extended = new Material[mats.Length + 1];
                for (int m = 0; m < mats.Length; m++) extended[m] = mats[m];
                extended[mats.Length] = rim;
                r.sharedMaterials = extended;
            }
        }

        private void CreateDecal(Transform parent, Material mat, float worldY, float worldDiameter, string decalName)
        {
            if (mat == null || worldDiameter <= 0f) return;

            GameObject go = new GameObject(decalName);
            go.layer = 2;   // Ignore Raycast : un disque au sol n'intercepte aucun clic.

            Transform t = go.transform;
            t.SetParent(parent, false);

            Vector3 p = parent.position;
            t.position = new Vector3(p.x, worldY, p.z);
            t.rotation = Quaternion.identity;

            // Taille MONDE voulue sur chaque axe, quelle que soit l'echelle des parents.
            // Un pion est enfant de sa case, et une case peut avoir une echelle non
            // uniforme : diviser par un seul axe donnait une ellipse au lieu d'un cercle.
            // On mesure donc l'echelle monde reelle axe par axe, et on la compense.
            t.localScale = Vector3.one;
            Vector3 world = t.lossyScale;
            t.localScale = new Vector3(
                worldDiameter / SafeAxis(world.x),
                worldDiameter / SafeAxis(world.y),
                worldDiameter / SafeAxis(world.z));

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = GetQuad();

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            go.AddComponent<ReadabilityDecal>();
        }

        private static float SafeAxis(float v)
        {
            v = Mathf.Abs(v);
            return (v > 0.00001f) ? v : 1f;
        }

        /// <summary>
        /// Hauteur monde du dessus des cases, mesuree une fois sur les plaines et les
        /// deserts (les cases les plus plates). On prend la plus basse : un caillou pose
        /// sur une case ne doit pas fausser la mesure.
        /// </summary>
        private float GetSurfaceY(float fallback)
        {
            if (surfaceHeightOverride != 0f) return surfaceHeightOverride;
            if (_surfaceKnown) return _surfaceY;

            BoardController board = BoardController.instance;
            if (board == null || board.HexagonsInBoard == null) return fallback;

            List<Hexagon> hexes = board.HexagonsInBoard;
            float best = float.MaxValue;
            int found = 0;

            for (int i = 0; i < hexes.Count && found < 12; i++)
            {
                Hexagon hex = hexes[i];
                if (hex == null || hex.level != 0) continue;
                if (hex.type != TypeOfHex.plain && hex.type != TypeOfHex.desert) continue;

                // Tampon a part : cette mesure peut etre demandee EN PLEIN milieu de la
                // decoration d'un pion, dont la liste _renderers ne doit pas etre ecrasee.
                CollectRenderers(hex.transform, false, _surfaceScratch);
                if (_surfaceScratch.Count == 0) continue;

                float top = float.MinValue;
                for (int r = 0; r < _surfaceScratch.Count; r++)
                {
                    float y = _surfaceScratch[r].bounds.max.y;
                    if (y > top) top = y;
                }

                if (top < best) best = top;
                found++;
            }

            _surfaceScratch.Clear();

            if (found == 0) return fallback;

            _surfaceY = best;
            _surfaceKnown = true;
            return best;
        }

        private static float BoardScale()
        {
            BoardController board = BoardController.instance;
            if (board == null) return 1f;

            float s = board.transform.lossyScale.x;
            return (s > 0.00001f) ? s : 1f;
        }

        private static float HexWidth()
        {
            return HexWidthBoardUnits * BoardScale();
        }

        // =================================================================
        //  MATERIAUX PARTAGES
        // =================================================================
        private void BuildSharedMaterials()
        {
            if (_rimShader != null)
            {
                _rimPlayer = new Material(_rimShader) { name = "Rim (joueur)" };
                _rimEnemy = new Material(_rimShader) { name = "Rim (ennemi)" };
                _rimPlayerBuilding = new Material(_rimShader) { name = "Rim (batiment joueur)" };
                _rimEnemyBuilding = new Material(_rimShader) { name = "Rim (batiment ennemi)" };
            }

            if (_decalShader != null)
            {
                _shadowMat = new Material(_decalShader) { name = "Ombre de contact" };
                _shadowFlyingMat = new Material(_decalShader) { name = "Ombre de contact (vol)" };
                _ringPlayer = new Material(_decalShader) { name = "Anneau (joueur)" };
                _ringEnemy = new Material(_decalShader) { name = "Anneau (ennemi)" };

                Texture2D shadowTex = GetShadowTexture();
                Texture2D ringTex = GetRingTexture();

                SetupDecal(_shadowMat, shadowTex, false);
                SetupDecal(_shadowFlyingMat, shadowTex, false);
                SetupDecal(_ringPlayer, ringTex, true);
                SetupDecal(_ringEnemy, ringTex, true);
            }

            RefreshMaterials();
        }

        private static void SetupDecal(Material mat, Texture2D tex, bool additive)
        {
            mat.SetTexture(MainTexId, tex);
            mat.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
            mat.SetFloat(DstBlendId, additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        }

        /// <summary>Recopie les reglages de l'inspecteur dans tous les materiaux deja crees.</summary>
        private void RefreshMaterials()
        {
            SetRim(_rimPlayer, playerColor, rimStrength);
            SetRim(_rimEnemy, enemyColor, rimStrength);
            SetRim(_rimPlayerBuilding, playerColor, buildingRimStrength);
            SetRim(_rimEnemyBuilding, enemyColor, buildingRimStrength);

            if (_shadowMat != null) _shadowMat.SetColor(ColorId, new Color(0f, 0f, 0f, shadowOpacity));
            if (_shadowFlyingMat != null) _shadowFlyingMat.SetColor(ColorId, new Color(0f, 0f, 0f, flyingShadowOpacity));

            if (_ringPlayer != null) _ringPlayer.SetColor(ColorId, RingColor(playerColor));
            if (_ringEnemy != null) _ringEnemy.SetColor(ColorId, RingColor(enemyColor));

            for (int i = 0; i < _calmMaterials.Count; i++)
                if (_calmMaterials[i] != null)
                    ApplyCalmParams(_calmMaterials[i], _calmTypeBrightness[i],
                                    _calmBaseColor[i], _calmTint[i]);
        }

        private void SetRim(Material mat, Color color, float strength)
        {
            if (mat == null) return;

            mat.SetColor(RimColorId, color);
            mat.SetFloat(RimPowerId, rimPower);
            mat.SetFloat(RimStrengthId, strength);
        }

        private Color RingColor(Color team)
        {
            return new Color(team.r * ringIntensity, team.g * ringIntensity, team.b * ringIntensity, 1f);
        }

        // =================================================================
        //  GEOMETRIE ET TEXTURES GENEREES (une seule fois pour toute la partie)
        // =================================================================
        private static Mesh GetQuad()
        {
            if (_quad != null) return _quad;

            _quad = new Mesh { name = "ReadabilityQuad" };
            _quad.vertices = new Vector3[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3( 0.5f, 0f, -0.5f),
                new Vector3( 0.5f, 0f,  0.5f),
                new Vector3(-0.5f, 0f,  0.5f),
            };
            _quad.uv = new Vector2[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            };
            _quad.normals = new Vector3[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            _quad.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            _quad.RecalculateBounds();
            return _quad;
        }

        /// <summary>
        /// Disque rond : plein jusqu'a 70% du rayon, puis un bord adouci. Un simple
        /// degrade du centre vers le bord se lisait comme une tache floue sans forme ;
        /// un disque au bord net mais doux se lit comme une ombre ronde.
        /// </summary>
        private static Texture2D GetShadowTexture()
        {
            if (_shadowTex != null) return _shadowTex;

            const int size = 64;
            _shadowTex = NewTexture(size, "ReadabilityShadow");

            Color32[] pixels = new Color32[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    float a = 1f - Smooth(0.7f, 1f, r);

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            _shadowTex.SetPixels32(pixels);
            _shadowTex.Apply(false, true);
            return _shadowTex;
        }

        /// <summary>Anneau net au bord, avec un voile tres leger a l'interieur.</summary>
        private static Texture2D GetRingTexture()
        {
            if (_ringTex != null) return _ringTex;

            const int size = 128;
            _ringTex = NewTexture(size, "ReadabilityRing");

            Color32[] pixels = new Color32[size * size];
            float half = size * 0.5f;

            const float radius = 0.84f;
            const float halfWidth = 0.05f;
            const float soft = 0.05f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    float band = 1f - Smooth(halfWidth, halfWidth + soft, Mathf.Abs(r - radius));
                    float fill = 0.16f * (1f - Smooth(radius - 0.15f, radius, r));
                    float a = Mathf.Max(band, fill);

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            }

            _ringTex.SetPixels32(pixels);
            _ringTex.Apply(false, true);
            return _ringTex;
        }

        /// <summary>
        /// Le smoothstep des shaders : 0 avant edge0, 1 apres edge1, une courbe douce
        /// entre les deux. ATTENTION : Mathf.SmoothStep de Unity n'est PAS cette fonction
        /// - c'est une interpolation entre ses deux premiers arguments. L'utiliser ici
        /// donnait un alpha presque constant, donc un carre plein au lieu d'un cercle.
        /// </summary>
        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static Texture2D NewTexture(int size, string textureName)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = textureName;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }
    }
}

// =============================================================================
// NOTE D'OPTIMISATION
// -----------------------------------------------------------------------------
// - Aucun Update : tout se passe a la creation d'une case ou d'un pion.
// - Plateau : une copie calme par materiau de terrain (une dizaine en tout), pas une
//   par case. Les cases qui partagent un materiau continuent a le partager.
// - Lisere : une passe additive de plus par pion et par batiment, sans texture.
// - Ombre et anneau : deux quads par pion, un seul maillage et deux textures
//   (64x64 et 128x128) generees une fois et partagees.
// - Les tampons de GetComponentsInChildren sont des listes reutilisees. Les seules
//   allocations sont les tableaux de materiaux a la creation d'un objet.
// - Aucun post-effet, aucune passe plein ecran : compatible WebGL / GLES3.
// =============================================================================
