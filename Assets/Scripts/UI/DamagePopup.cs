using System.Collections.Generic;
using UnityEngine;

namespace MNLTHII.UI
{
    /// <summary>Qui encaisse, et donc de quelle couleur est la nouvelle.</summary>
    public enum DamageKind
    {
        /// <summary>Un ennemi ou un Shofar prend le coup : bonne nouvelle, en or.</summary>
        DealtToEnemy,

        /// <summary>La Base, un Tank ou un batiment prend le coup : mauvaise, en rouge.</summary>
        TakenByPlayer,

        /// <summary>Soin.</summary>
        Healed,

        /// <summary>Revenu d'une usine : le chiffre qui dit d'ou vient l'argent.</summary>
        Income
    }

    /// <summary>
    /// Le chiffre qui s'envole au-dessus de ce qui vient d'encaisser.
    ///
    /// POURQUOI C'EST LA PIECE QUI MANQUAIT
    ///
    /// Le jeu avait deja un flash rouge et une barre de vie. Ni l'un ni l'autre ne dit
    /// COMBIEN. Un flash dit "quelque chose s'est passe" ; une barre dit "il en reste
    /// tant" - et pour la lire il faut l'avoir regardee juste avant. Entre les deux,
    /// personne ne repond a la seule question que le joueur se pose : est-ce que mon
    /// tir a servi ?
    ///
    /// C'est encore plus vrai pour les Shofars. Leur bouclier divise les degats par
    /// deux tant qu'il tient : sans chiffre a l'ecran, frapper un Shofar protege et un
    /// Shofar ebranle produit exactement la meme image. Toute la boucle centrale du
    /// jeu devient invisible.
    ///
    /// Un seul point d'entree, deux points d'appel : ApplyDamage du pion et ApplyDamage
    /// de l'hexagone. Tout ce qui blesse quoi que ce soit dans ce jeu passe par l'un
    /// des deux - coup de Tank, tir de Bunker, attaque ennemie, contrecoup de Shofar.
    /// Rien ne peut donc etre oublie.
    ///
    /// Aucun prefab, aucune police a fournir : le panneau se construit tout seul, comme
    /// FloatingHealthBar, et TextMeshPro utilise sa police par defaut. Le texte n'est
    /// fait que de chiffres et d'un signe - de l'ASCII pur - donc aucun besoin de
    /// glyphes hebreux ici.
    ///
    /// Note d'optimisation : les panneaux sont POOLES et ne sont jamais detruits. Un
    /// combat produit des dizaines de chiffres par tour ; en creer et en detruire un a
    /// chaque coup ferait travailler le ramasse-miettes en plein milieu de l'action,
    /// et c'est exactement le moment ou une saccade se voit. Le texte passe par
    /// SetText(format, valeur), qui ecrit dans le tampon de char de TMP sans allouer
    /// de string. Camera.main est resolue une fois et partagee.
    /// </summary>
    public class DamagePopup : MonoBehaviour
    {
        // =================================================================
        //  REGLAGES - constants, pour qu'aucune scene n'ait besoin d'etre cablee
        // =================================================================
        private const float Lifetime = 0.95f;
        private const float RiseDistance = 1.1f;
        private const float StartHeight = 1.35f;

        // Le panneau est defini en pixels puis mis a l'echelle en unites monde, comme
        // la barre de vie : meme methode, memes proportions a l'ecran.
        private const float WorldWidth = 1.0f;
        private const float PixelWidth = 120f;
        private const float PixelHeight = 48f;
        private const float FontSize = 40f;

        // Ecart lateral entre deux chiffres qui partiraient du meme endroit. Sans lui,
        // quatre tirs de Bunker sur la meme unite empilent quatre "10" illisibles.
        private const float FanOut = 0.22f;

        private static readonly Color ColorDealt = new Color(1f, 0.82f, 0.36f);
        private static readonly Color ColorTaken = new Color(1f, 0.32f, 0.36f);
        private static readonly Color ColorHeal = new Color(0.42f, 0.92f, 0.62f);

        /// <summary>Le revenu d'une usine : cyan, la couleur de l'Energie dans le HUD.</summary>
        private static readonly Color ColorIncome = new Color(0.30f, 0.94f, 0.86f);

        // =================================================================
        //  POOL PARTAGE
        // =================================================================
        private static readonly List<DamagePopup> Pool = new List<DamagePopup>(24);
        private static Transform _poolRoot;
        private static Camera _camera;
        private static int _spawnCounter;

        // =================================================================
        //  ETAT D'UN PANNEAU
        // =================================================================
        private Transform _transform;
        private CanvasGroup _group;
        private TMPro.TextMeshProUGUI _text;

        private Vector3 _origin;
        private float _elapsed;
        private bool _alive;

        // =================================================================
        //  API
        // =================================================================
        /// <summary>
        /// Fait monter un chiffre au-dessus d'un point du monde. amount est toujours
        /// donne positif : c'est kind qui decide du signe affiche et de la couleur.
        /// </summary>
        public static void Show(Vector3 worldPosition, int amount, DamageKind kind)
        {
            if (amount <= 0) return;

            DamagePopup popup = Rent();
            if (popup == null) return;

            popup.Begin(worldPosition, amount, kind);
        }

        // =================================================================
        //  POOL
        // =================================================================
        private static DamagePopup Rent()
        {
            for (int i = 0; i < Pool.Count; i++)
            {
                DamagePopup candidate = Pool[i];
                if (candidate != null && !candidate._alive) return candidate;
            }

            DamagePopup created = Build();
            if (created != null) Pool.Add(created);
            return created;
        }

        private static DamagePopup Build()
        {
            if (_poolRoot == null)
            {
                GameObject root = new GameObject("DamagePopups");
                _poolRoot = root.transform;
            }

            // ATTENTION : Canvas exige un RectTransform, et l'ajouter apres coup
            // REMPLACE le Transform de l'objet - toute reference prise avant devient
            // invalide. On declare donc les composants des la creation, comme dans
            // FloatingHealthBar, et on ne lit le Transform qu'ensuite.
            GameObject go = new GameObject("DamagePopup", typeof(Canvas), typeof(CanvasGroup));
            go.transform.SetParent(_poolRoot, false);

            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            // Au-dessus des barres de vie : un chiffre masque par une barre ne sert a rien.
            canvas.sortingOrder = 120;

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(PixelWidth, PixelHeight);

            float scale = WorldWidth / PixelWidth;
            rect.localScale = new Vector3(scale, scale, scale);

            DamagePopup popup = go.AddComponent<DamagePopup>();
            popup._transform = rect;

            popup._group = go.GetComponent<CanvasGroup>();
            popup._group.alpha = 0f;
            popup._group.blocksRaycasts = false;
            popup._group.interactable = false;

            GameObject textObject = new GameObject("Valeur");
            textObject.transform.SetParent(rect, false);

            TMPro.TextMeshProUGUI text = textObject.AddComponent<TMPro.TextMeshProUGUI>();
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.fontSize = FontSize;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            // Jamais de droite-a-gauche ici : un nombre passe en RTL voit ses chiffres
            // s'inverser, et 15 devient 51.
            text.isRightToLeftText = false;

            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            popup._text = text;

            go.SetActive(false);
            return popup;
        }

        // =================================================================
        //  CYCLE DE VIE D'UN PANNEAU
        // =================================================================
        private void Begin(Vector3 worldPosition, int amount, DamageKind kind)
        {
            if (_camera == null) _camera = Camera.main;

            // Eventail : chaque nouveau chiffre part un peu a cote du precedent.
            _spawnCounter++;
            float offset = ((_spawnCounter % 4) - 1.5f) * FanOut;

            _origin = worldPosition + Vector3.up * StartHeight + Vector3.right * offset;

            _transform.position = _origin;
            FaceCamera();

            Color tint;
            switch (kind)
            {
                case DamageKind.TakenByPlayer: tint = ColorTaken; break;
                case DamageKind.Healed: tint = ColorHeal; break;
                case DamageKind.Income: tint = ColorIncome; break;
                default: tint = ColorDealt; break;
            }

            _text.color = tint;

            if (kind == DamageKind.Healed || kind == DamageKind.Income) _text.SetText("+{0}", amount);
            else _text.SetText("-{0}", amount);

            _elapsed = 0f;
            _alive = true;
            _group.alpha = 1f;

            gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            if (!_alive) return;

            _elapsed += Time.deltaTime;

            float t = _elapsed / Lifetime;

            if (t >= 1f)
            {
                _alive = false;
                _group.alpha = 0f;
                gameObject.SetActive(false);
                return;
            }

            // Montee qui ralentit : le chiffre jaillit puis se pose. Une montee
            // lineaire donne un ascenseur, pas un impact.
            float rise = 1f - (1f - t) * (1f - t);
            _transform.position = _origin + Vector3.up * (RiseDistance * rise);

            // La disparition n'occupe que le dernier tiers : le chiffre doit etre
            // parfaitement lisible pendant les deux premiers.
            _group.alpha = (t < 0.66f) ? 1f : 1f - ((t - 0.66f) / 0.34f);

            FaceCamera();
        }

        /// <summary>
        /// Le panneau garde l'orientation de la camera. On copie sa rotation plutot
        /// que de viser sa position : viser donnerait un texte incline differemment
        /// selon l'endroit de l'ecran, et deux chiffres cote a cote ne seraient plus
        /// paralleles.
        /// </summary>
        private void FaceCamera()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null) return;
            }

            _transform.rotation = _camera.transform.rotation;
        }
    }
}
