using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MNLTHII.Rules;

namespace MNLTHII.Managers
{
    /// <summary>
    /// LE CORDON.
    ///
    /// Un arc electrique relie chaque ennemi au Shofar qui l'a deploye. Quand cet
    /// ennemi tombe, l'arc se decharge : il remonte le long du cordon jusqu'au Shofar,
    /// qui encaisse visiblement le coup.
    ///
    /// POURQUOI CETTE CHOSE EXISTE
    ///
    /// La regle centrale du jeu est invisible en jouant. Tuer un ennemi blesse le
    /// Shofar qui l'a envoye - c'est ce qui use sa reserve, et c'est au bout de quatre
    /// morts que son bouclier se brise et que la fenetre offensive s'ouvre. Mais le
    /// joueur tue un ennemi ici, et les points de vie baissent la-bas, a huit cases de
    /// distance, souvent hors de l'ecran. Rien ne relie les deux. Resultat : on defend
    /// correctement pendant dix tours sans jamais comprendre qu'on est en train de
    /// gagner.
    ///
    /// Le cordon rend ce lien PERMANENT et VISIBLE. On voit d'un regard combien
    /// d'ennemis dependent de quel Shofar, donc lequel on est en train d'user. Et a
    /// chaque mort, la decharge trace le chemin du degat jusqu'a sa source.
    ///
    /// Note d'optimisation : un cordon par ennemi, mis en pool, jamais detruit. Les
    /// points de chaque arc vivent dans un Vector3[] alloue une fois et reecrit sur
    /// place, puis pousse d'un bloc par SetPositions. Le bruit vient de PerlinNoise,
    /// qui ne fait aucune allocation. Rien n'est alloue dans LateUpdate.
    /// </summary>
    public class PortalTether : MonoBehaviour
    {
        public static PortalTether Instance;

        private const int MaxTethers = 32;

        // =================================================================
        //  REGLAGES
        // =================================================================
        [Header("Aspect")]
        /// <summary>Laisse vide : un materiau non eclaire est genere au demarrage.</summary>
        public Material lineMaterial;

        /// <summary>Couleur au repos, cote ennemi puis cote Shofar.</summary>
        public Color enemyColor = new Color(1f, 0.42f, 0.22f, 0.55f);
        public Color portalColor = new Color(1f, 0.72f, 0.30f, 0.16f);

        /// <summary>Couleur de la decharge : un eclair blanc-or, impossible a manquer.</summary>
        public Color dischargeColor = new Color(1f, 0.95f, 0.72f, 1f);

        public float width = 0.07f;
        public float dischargeWidth = 0.26f;

        /// <summary>Plus il y a de segments, plus l'eclair est detaille - et plus il coute.</summary>
        [Range(4, 32)] public int segments = 14;

        /// <summary>Amplitude du tremblement, en unites de monde.</summary>
        public float jitter = 0.22f;

        /// <summary>Hauteur de l'arc en son milieu : sans elle le cordon rase le sol.</summary>
        public float arcHeight = 1.1f;

        public float animationSpeed = 7f;

        [Header("Ancrages")]
        public float enemyAnchorHeight = 0.6f;
        public float portalAnchorHeight = 1.4f;

        [Header("Decharge")]
        /// <summary>
        /// Volontairement court : le chiffre de degat parait a l'instant de la mort, et
        /// une decharge lente ferait arriver l'impact longtemps apres lui. A cette
        /// duree, l'oeil lit les deux comme un seul evenement.
        /// </summary>
        public float dischargeDuration = 0.32f;

        /// <summary>Le modele du Shofar encaisse : il est chasse puis revient.</summary>
        public float punchScale = 1.22f;
        public float punchDuration = 0.28f;

        [Header("Diagnostic")]
        public int activeTethers = 0;

        // =================================================================
        //  ETAT INTERNE
        // =================================================================
        /// <summary>Un cordon : sa ligne, ses points, et les deux bouts qu'il relie.</summary>
        private class Tether
        {
            public GameObject gameObject;
            public LineRenderer line;
            public Vector3[] points;

            public PawnController enemy;
            public Hexagon portal;

            public float seed;
            public bool inUse;

            /// <summary>Progression de la decharge, ou -1 quand le cordon est au repos.</summary>
            public float discharge;

            /// <summary>Derniere position connue de l'ennemi : il n'existe plus pendant la decharge.</summary>
            public Vector3 lastEnemyPoint;

            /// <summary>PV reellement retires au Shofar par cette mort. Peut valoir zero.</summary>
            public int impactAmount;
        }

        private readonly List<Tether> _tethers = new List<Tether>(MaxTethers);
        private Transform _root;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(this); return; }

            GameObject root = new GameObject("PortalTethers");
            _root = root.transform;

            EnsureMaterial();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_root != null) Destroy(_root.gameObject);
        }

        // =================================================================
        //  MATERIAU GENERE
        // =================================================================
        /// <summary>
        /// Materiau non eclaire, genere si l'inspecteur n'en fournit pas. Le cordon
        /// doit marcher sans qu'on ait rien a preparer dans le projet : une
        /// fonctionnalite d'affichage qui exige un reglage manuel finit toujours par
        /// etre livree sans ce reglage.
        /// </summary>
        private void EnsureMaterial()
        {
            if (lineMaterial != null) return;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");

            if (shader == null)
            {
                Debug.LogWarning("[Cordon] Aucun shader non eclaire trouve : assigne lineMaterial dans l'inspecteur.");
                return;
            }

            lineMaterial = new Material(shader);
            lineMaterial.name = "PortalTether";
        }

        // =================================================================
        //  API
        // =================================================================
        /// <summary>
        /// Recense les ennemis vivants et ouvre un cordon pour chacun. Appele aux
        /// changements de tour, pas image par image : un ennemi n'apparait ni ne
        /// disparait entre deux images.
        /// </summary>
        public void Refresh()
        {
            BoardController board = BoardController.instance;
            if (board == null || board.PawnsInBoard == null) return;

            List<PawnController> pawns = board.PawnsInBoard;

            for (int i = 0; i < pawns.Count; i++)
            {
                PawnController pawn = pawns[i];
                if (pawn == null || !pawn.IsEnemy || pawn.currentHP <= 0) continue;
                if (!pawn.HasOriginPortal) continue;
                if (FindTether(pawn) != null) continue;

                Hexagon portal = FindPortalAt(pawn.originPortalQ, pawn.originPortalR);
                if (portal == null || portal.currentHP <= 0) continue;

                Attach(pawn, portal);
            }
        }

        /// <summary>Ouvre un cordon entre un ennemi et son Shofar.</summary>
        public void Attach(PawnController enemy, Hexagon portal)
        {
            if (enemy == null || portal == null) return;
            if (FindTether(enemy) != null) return;

            Tether tether = Acquire();
            if (tether == null) return;

            tether.enemy = enemy;
            tether.portal = portal;
            tether.discharge = -1f;
            tether.seed = Random.Range(0f, 64f);
            tether.lastEnemyPoint = enemy.transform.position;

            tether.line.startColor = enemyColor;
            tether.line.endColor = portalColor;
            tether.line.startWidth = width;
            tether.line.endWidth = width;

            tether.gameObject.SetActive(true);
        }

        /// <summary>
        /// L'ennemi vient de tomber : son cordon se decharge dans le Shofar. Appele par
        /// PortalManager au moment exact ou le contrecoup est applique, pour que l'image
        /// et le chiffre racontent le meme evenement.
        /// </summary>
        public void Discharge(PawnController enemy, Hexagon portal, int amount)
        {
            Tether tether = FindTether(enemy);

            // Pas de cordon ouvert - un ennemi tue au tour meme de son apparition, par
            // exemple. On en ouvre un a la volee : la decharge est justement le moment
            // ou le joueur DOIT voir le lien.
            if (tether == null)
            {
                if (enemy == null || portal == null) return;

                Attach(enemy, portal);
                tether = FindTether(enemy);
                if (tether == null) return;
            }

            if (enemy != null) tether.lastEnemyPoint = enemy.transform.position;

            tether.impactAmount = amount;
            tether.discharge = 0f;
            tether.line.startColor = dischargeColor;
            tether.line.endColor = dischargeColor;
        }

        /// <summary>Referme tous les cordons : changement de partie, plateau recharge.</summary>
        public void ClearAll()
        {
            for (int i = 0; i < _tethers.Count; i++) Release(_tethers[i]);
            activeTethers = 0;
        }

        // =================================================================
        //  ANIMATION
        // =================================================================
        /// <summary>
        /// LateUpdate et non Update : les pions sont deplaces par DOTween pendant
        /// Update, donc lire leur position plus tot ferait trainer le cordon d'une
        /// image derriere l'unite.
        /// </summary>
        private void LateUpdate()
        {
            int active = 0;
            float time = Time.time * animationSpeed;

            for (int i = 0; i < _tethers.Count; i++)
            {
                Tether tether = _tethers[i];
                if (!tether.inUse) continue;

                // Un Shofar ferme n'a plus de cordon a tenir.
                if (tether.portal == null || tether.portal.currentHP <= 0)
                {
                    Release(tether);
                    continue;
                }

                bool discharging = (tether.discharge >= 0f);

                // L'ennemi a disparu sans passer par la decharge (retrait du plateau,
                // rechargement) : on referme proprement.
                if (!discharging && (tether.enemy == null || tether.enemy.currentHP <= 0))
                {
                    Release(tether);
                    continue;
                }

                Vector3 to = tether.portal.transform.position + Vector3.up * portalAnchorHeight;
                Vector3 from;

                if (discharging)
                {
                    tether.discharge += Time.deltaTime;

                    float duration = (dischargeDuration > 0.05f) ? dischargeDuration : 0.05f;
                    float t = tether.discharge / duration;

                    if (t >= 1f)
                    {
                        Impact(tether);
                        Release(tether);
                        continue;
                    }

                    // L'extremite cote ennemi remonte le cordon : l'eclair se resorbe
                    // DANS le Shofar au lieu de s'eteindre sur place. C'est ce
                    // mouvement qui dit "ce degat vient de la-bas".
                    float eased = t * t;
                    from = Vector3.Lerp(tether.lastEnemyPoint, to, eased);

                    float w = Mathf.Lerp(dischargeWidth, width, t);
                    tether.line.startWidth = w;
                    tether.line.endWidth = w;
                }
                else
                {
                    from = tether.enemy.transform.position + Vector3.up * enemyAnchorHeight;
                    tether.lastEnemyPoint = from;
                }

                Trace(tether, from, to, time, discharging);
                active++;
            }

            activeTethers = active;
        }

        /// <summary>Remplit les points de l'arc et les pousse d'un seul bloc.</summary>
        private void Trace(Tether tether, Vector3 from, Vector3 to, float time, bool discharging)
        {
            Vector3[] points = tether.points;
            int count = points.Length;
            if (count < 2) return;

            Vector3 axis = to - from;
            float length = axis.magnitude;
            if (length < 0.001f) { axis = Vector3.forward; length = 0.001f; }

            Vector3 direction = axis / length;

            // Base perpendiculaire stable. Cross avec l'axe vertical suffit tant que le
            // cordon n'est pas exactement vertical ; sinon on bascule sur l'axe avant.
            Vector3 right = Vector3.Cross(direction, Vector3.up);
            if (right.sqrMagnitude < 0.001f) right = Vector3.Cross(direction, Vector3.forward);
            right.Normalize();

            Vector3 up = Vector3.Cross(right, direction);

            float amplitude = discharging ? jitter * 2.2f : jitter;
            float arc = discharging ? arcHeight * 0.55f : arcHeight;

            points[0] = from;
            points[count - 1] = to;

            for (int i = 1; i < count - 1; i++)
            {
                float t = (float)i / (count - 1);

                // Enveloppe en cloche : le tremblement et l'arc sont nuls aux deux
                // ancrages. Sans elle, l'eclair se decrocherait de l'unite et du Shofar.
                float envelope = Mathf.Sin(t * Mathf.PI);

                float nx = Mathf.PerlinNoise(tether.seed + i * 0.73f, time) * 2f - 1f;
                float ny = Mathf.PerlinNoise(tether.seed + 31.7f + i * 0.73f, time) * 2f - 1f;

                Vector3 point = from + direction * (length * t);
                point += up * (arc * envelope);
                point += right * (nx * amplitude * envelope);
                point += up * (ny * amplitude * envelope);

                points[i] = point;
            }

            tether.line.positionCount = count;
            tether.line.SetPositions(points);
        }

        // =================================================================
        //  IMPACT SUR LE SHOFAR
        // =================================================================
        /// <summary>
        /// La decharge est arrivee. Le Shofar a DEJA perdu ses PV - c'est PortalManager
        /// qui s'en charge, a l'instant de la mort. Ici on ne touche a aucune regle : on
        /// rend le coup visible.
        /// </summary>
        private void Impact(Tether tether)
        {
            Hexagon portal = tether.portal;
            if (portal == null) return;

            // Un Shofar deja au plancher de ses PV ne perd rien de plus : le
            // contrecoup s'y arrete. La mort compte quand meme - elle rapproche la
            // rupture du bouclier - mais l'impact doit etre plus sourd, sinon l'image
            // promet des degats que la barre de vie ne montrera pas.
            bool wounded = (tether.impactAmount > 0);

            FXManager fx = FXManager.Instance;
            if (fx != null)
            {
                fx.SpawnHitFX(portal.transform.position + Vector3.up * portalAnchorHeight);
                if (wounded) fx.SpawnDestructionFX(portal.transform.position + Vector3.up * 0.4f);
            }

            portal.RefreshHealthBar();

            StartCoroutine(Punch(portal, wounded ? 1f : 0.4f));
        }

        /// <summary>
        /// Le modele encaisse : il gonfle d'un coup puis revient. On scrute le MESH,
        /// jamais l'hexagone lui-meme - les pions sont enfants de leur hexagone, et
        /// mettre celui-ci a l'echelle ferait enfler tout ce qui se tient dessus.
        /// </summary>
        private IEnumerator Punch(Hexagon portal, float strength)
        {
            MeshRenderer renderer = portal.GetComponentInChildren<MeshRenderer>();
            if (renderer == null) yield break;

            Transform model = renderer.transform;

            // Le mesh est porte par l'hexagone lui-meme : pas de secousse possible sans
            // emporter ce qui s'y trouve. Le flash rouge de Hexagon reste, lui.
            if (model == portal.transform) yield break;

            Vector3 baseScale = model.localScale;
            float duration = (punchDuration > 0.05f) ? punchDuration : 0.05f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Une seule oscillation : monte vite, redescend en douceur.
                float pulse = Mathf.Sin(t * Mathf.PI);
                model.localScale = baseScale * (1f + (punchScale - 1f) * pulse * strength);

                yield return null;
            }

            model.localScale = baseScale;
        }

        // =================================================================
        //  POOL
        // =================================================================
        private Tether Acquire()
        {
            for (int i = 0; i < _tethers.Count; i++)
                if (!_tethers[i].inUse)
                {
                    _tethers[i].inUse = true;
                    return _tethers[i];
                }

            if (_tethers.Count >= MaxTethers) return null;

            Tether created = Build();
            created.inUse = true;
            _tethers.Add(created);
            return created;
        }

        private Tether Build()
        {
            int count = Mathf.Clamp(segments, 4, 32);

            GameObject go = new GameObject("Tether");
            go.transform.SetParent(_root, false);
            go.SetActive(false);

            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = count;
            line.numCapVertices = 2;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            if (lineMaterial != null) line.sharedMaterial = lineMaterial;

            Tether tether = new Tether();
            tether.gameObject = go;
            tether.line = line;
            tether.points = new Vector3[count];
            tether.discharge = -1f;

            return tether;
        }

        private void Release(Tether tether)
        {
            tether.inUse = false;
            tether.enemy = null;
            tether.portal = null;
            tether.discharge = -1f;

            if (tether.gameObject != null) tether.gameObject.SetActive(false);
        }

        private Tether FindTether(PawnController enemy)
        {
            if (enemy == null) return null;

            for (int i = 0; i < _tethers.Count; i++)
            {
                Tether tether = _tethers[i];
                if (tether.inUse && tether.enemy == enemy) return tether;
            }
            return null;
        }

        private static Hexagon FindPortalAt(int q, int r)
        {
            BoardController board = BoardController.instance;
            if (board == null || board.HexagonsInBoard == null) return null;

            List<Hexagon> hexes = board.HexagonsInBoard;
            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex == null || hex.type != TypeOfHex.portal || hex.positionInTheBoard == null) continue;
                if (hex.positionInTheBoard.q == q && hex.positionInTheBoard.r == r) return hex;
            }
            return null;
        }
    }
}
