using UnityEngine;
using UnityEngine.Rendering;

namespace MNLTHII.Managers
{
    /// <summary>
    /// UN TRAIT DE TIR VISIBLE : du canon a la cible, un eclair bref qui s'estompe.
    ///
    /// Le Bunker tirait sans que rien ne relie le tir a sa cible : on voyait un impact
    /// apparaitre quelque part, sans savoir qui avait tire ni sur quoi. L'ancien laser
    /// avait ete retire parce qu'il ne s'affichait pas - il cherchait un shader
    /// (Legacy Particles) qui n'est pas inclus dans le build, donc invisible.
    ///
    /// Celui-ci utilise le shader du projet (Resources/Shaders/MNLTH_GroundDecal), qui
    /// est toujours inclus, en mode additif : un trait lumineux qui se lit sur
    /// n'importe quel fond.
    ///
    /// Utilisation : ShotTracer.Fire(depart, arrivee, couleur). Rien a poser dans la
    /// scene, le composant se cree au premier tir.
    ///
    /// Note d'optimisation : huit LineRenderer crees une fois et recycles. Pendant un
    /// tir, un seul LateUpdate fait s'estomper les traits actifs, via un
    /// MaterialPropertyBlock reutilise. Aucune allocation par tir. Quand plus rien
    /// n'est actif, le composant se desactive.
    /// </summary>
    public class ShotTracer : MonoBehaviour
    {
        private static ShotTracer _instance;

        private const int PoolSize = 8;

        // Premier reglage beaucoup trop bref (0.35 s en tout) : le trait apparaissait
        // et disparaissait avant que l'oeil ne le trouve. Maintenant le trait VOYAGE
        // du canon a la cible, reste allume un instant, puis s'eteint - plus d'une
        // seconde en tout.
        [Tooltip("Epaisseur du trait au depart et a l'arrivee, en unites monde.")]
        public float startWidth = 0.13f;
        public float endWidth = 0.08f;

        [Tooltip("Temps pour que le trait aille du canon a la cible, en secondes reelles.")]
        public float travelTime = 0.3f;

        [Tooltip("Temps pendant lequel le trait reste pleinement allume une fois arrive.")]
        public float holdTime = 0.35f;

        [Tooltip("Temps d'extinction.")]
        public float fadeTime = 0.5f;

        private LineRenderer[] _lines;
        private float[] _bornAt;
        private Color[] _colors;
        private Vector3[] _starts;
        private Vector3[] _ends;
        private Material _material;
        private MaterialPropertyBlock _block;
        private int _next;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");

        /// <summary>
        /// Tire un trait de start a end, de cette couleur. Rend le temps (secondes
        /// reelles) que met le trait pour atteindre la cible : l'appelant peut attendre
        /// ce delai avant d'appliquer l'impact, pour que le coup tombe quand le trait
        /// arrive.
        /// </summary>
        public static float Fire(Vector3 start, Vector3 end, Color color)
        {
            if (_instance == null)
            {
                _instance = new GameObject("ShotTracer (auto)").AddComponent<ShotTracer>();
                if (!_instance.Build()) return 0f;
            }

            _instance.Shoot(start, end, color);
            return _instance.travelTime;
        }

        private bool Build()
        {
            Shader shader = Resources.Load<Shader>("Shaders/MNLTH_GroundDecal");
            if (shader == null)
            {
                Debug.LogWarning("[ShotTracer] Shader MNLTH_GroundDecal introuvable : les tirs ne seront pas traces.");
                return false;
            }

            _material = new Material(shader) { name = "Trait de tir" };
            _material.SetTexture(MainTexId, Texture2D.whiteTexture);
            _material.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
            _material.SetFloat(DstBlendId, (float)BlendMode.One);   // additif : il eclaire
            _material.renderQueue = 3100;

            _block = new MaterialPropertyBlock();
            _lines = new LineRenderer[PoolSize];
            _bornAt = new float[PoolSize];
            _colors = new Color[PoolSize];
            _starts = new Vector3[PoolSize];
            _ends = new Vector3[PoolSize];

            for (int i = 0; i < PoolSize; i++)
            {
                GameObject go = new GameObject("Trait" + i);
                go.layer = 2;
                go.transform.SetParent(transform, false);

                LineRenderer line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = _material;
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.alignment = LineAlignment.View;
                line.numCapVertices = 2;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.enabled = false;

                _lines[i] = line;
                _bornAt[i] = -100f;
            }

            return true;
        }

        private void Shoot(Vector3 start, Vector3 end, Color color)
        {
            if (_lines == null) return;

            LineRenderer line = _lines[_next];
            _bornAt[_next] = Time.unscaledTime;
            _colors[_next] = color;
            _starts[_next] = start;
            _ends[_next] = end;
            _next = (_next + 1) % PoolSize;

            line.startWidth = startWidth;
            line.endWidth = endWidth;
            line.SetPosition(0, start);
            line.SetPosition(1, start);   // il part du canon et s'allonge
            line.enabled = true;

            ApplyColor(line, color, 1f);
            enabled = true;
        }

        private void LateUpdate()
        {
            if (_lines == null) { enabled = false; return; }

            bool any = false;
            float now = Time.unscaledTime;
            float travel = Mathf.Max(0.01f, travelTime);
            float fade = Mathf.Max(0.01f, fadeTime);

            for (int i = 0; i < PoolSize; i++)
            {
                LineRenderer line = _lines[i];
                if (!line.enabled) continue;

                float age = now - _bornAt[i];

                if (age < travel)
                {
                    // 1. Le trait voyage : sa tete avance du canon vers la cible.
                    float k = age / travel;
                    line.SetPosition(0, _starts[i]);
                    line.SetPosition(1, Vector3.Lerp(_starts[i], _ends[i], k));
                    ApplyColor(line, _colors[i], 1f);
                }
                else if (age < travel + holdTime)
                {
                    // 2. Il relie le canon a la cible, pleinement allume.
                    line.SetPosition(1, _ends[i]);
                    ApplyColor(line, _colors[i], 1f);
                }
                else
                {
                    // 3. Il s'eteint.
                    float t = (age - travel - holdTime) / fade;
                    if (t >= 1f)
                    {
                        line.enabled = false;
                        continue;
                    }

                    line.SetPosition(1, _ends[i]);
                    ApplyColor(line, _colors[i], 1f - t * t);
                }

                any = true;
            }

            if (!any) enabled = false;
        }

        private void ApplyColor(LineRenderer line, Color color, float alpha)
        {
            line.GetPropertyBlock(_block);
            _block.SetColor(ColorId, new Color(color.r, color.g, color.b, color.a * alpha));
            line.SetPropertyBlock(_block);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_material != null) Destroy(_material);
        }
    }
}
