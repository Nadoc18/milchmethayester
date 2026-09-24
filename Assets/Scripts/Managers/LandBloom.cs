using System.Collections;
using UnityEngine;

namespace MNLTHII.Managers
{
    /// <summary>
    /// LA TERRE REFLEURIT.
    ///
    /// CE QUE C'EST
    ///
    /// Les deux derniers anneaux du plateau sont du desert nu : le glacis. Ce n'est
    /// pas un decor, c'est ce que le Yetzer a desseche. Chaque Shofar tient un
    /// sixieme de ce desert - la part de tarte qui lui fait face.
    ///
    /// Quand un Shofar tombe, SA part redevient ce qu'elle etait avant lui. La plaine
    /// reparait, et les deux sites que le glacis avait effaces sur cet axe ressortent
    /// du sol, intacts et a construire : une usine de Gaz, et une colline a deux cases
    /// du Shofar voisin.
    ///
    /// POURQUOI CETTE RECOMPENSE-LA
    ///
    /// C'est la seule recompense du jeu qui ne soit pas un chiffre. Fermer un Shofar
    /// ne fait pas monter un compteur : ca rend une terre, et cette terre sert. Une
    /// usine de plus, une position de siege contre le Shofar suivant, et des plaines
    /// ou poser des Tanks pour la suite de l'offensive.
    ///
    /// Elle corrige aussi la forme de la partie. Avant, les six Shofars coutaient le
    /// meme effort du premier au dernier, et la fin s'etirait. Maintenant le premier
    /// abattu rend le deuxieme plus facile : la partie s'accelere vers sa fin au lieu
    /// de s'y trainer. Et le joueur VOIT sa victoire - la carte verdit secteur par
    /// secteur.
    ///
    /// COMMENT C'EST FAIT
    ///
    /// Sans aucun etat conserve. A chaque fin de tour, on regarde les six pointes :
    /// celles dont le Shofar est tombe font refleurir leur secteur. L'operation ne
    /// touche QUE le desert du glacis encore vierge, donc la repasser ne defait
    /// jamais rien - une usine deja batie sur une case rendue n'est plus du desert, et
    /// se fait ignorer.
    ///
    /// C'est aussi ce qui la rend juste apres un chargement de partie : rien n'est
    /// sauvegarde, tout se recalcule a partir du plateau lui-meme.
    ///
    /// Une case occupee par un pion est simplement laissee pour le tour suivant : un
    /// ennemi debout la ne doit pas se retrouver sur une colline, ou plus personne ne
    /// peut marcher.
    ///
    /// Note d'optimisation : douze cases par secteur tombe, une fois par tour. Aucune
    /// allocation ; les coordonnees des six pointes sont relevees une seule fois.
    /// </summary>
    public class LandBloom : MonoBehaviour
    {
        public static LandBloom Instance;

        [Header("Rythme")]
        [Tooltip("Temps entre deux cases qui refleurissent.")]
        public float cellDelay = 0.12f;

        [Tooltip("Temps de contemplation, une fois le secteur rendu.")]
        public float holdAfter = 1.1f;

        [Tooltip("La camera descend sur le secteur qui refleurit.")]
        public bool focusCamera = true;

        private static readonly Color BannerColor = new Color(0.42f, 0.92f, 0.62f);

        // Les six pointes, relevees une fois. Elles ne bougent jamais - meme mortes,
        // elles continuent de decouper le plateau en six secteurs.
        private static readonly int[] CornerQ = new int[6];
        private static readonly int[] CornerR = new int[6];
        private static readonly int[] CornerS = new int[6];
        private static bool _cornersReady;


        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private static void EnsureCorners()
        {
            if (_cornersReady) return;
            _cornersReady = true;

            for (int i = 0; i < 6; i++)
                MapGenerator.GetPortalCorner(i, out CornerQ[i], out CornerR[i], out CornerS[i]);
        }

        // =================================================================
        //  LE PASSAGE DE FIN DE TOUR
        // =================================================================
        /// <summary>
        /// Fait refleurir tous les secteurs dont le Shofar est tombe. Ne rend la main
        /// qu'une fois la ceremonie finie ; retourne immediatement s'il n'y a rien a
        /// rendre, ce qui est le cas de la grande majorite des tours.
        /// </summary>
        public IEnumerator ProcessBlooms()
        {
            BoardController board = BoardController.instance;
            if (board == null || board.HexagonsInBoard == null) yield break;

            EnsureCorners();

            for (int i = 0; i < 6; i++)
            {
                if (!IsPortalFallen(board, i)) continue;
                yield return BloomSector(board, i);
            }
        }

        /// <summary>
        /// La floraison suit l'allure choisie et se laisse interrompre comme le reste
        /// de la fin de tour. Elle ne rend pas moins de terrain pour autant : Restore
        /// a deja fait son travail quand l'attente tombe.
        /// </summary>
        private readonly PaceWait _pace = new PaceWait();

        /// <summary>
        /// Ce Shofar est-il tombe ? Sa case porte alors une epave (type Destroyed), ou
        /// un Shofar a zero PV le temps que la destruction s'acheve.
        /// </summary>
        private static bool IsPortalFallen(BoardController board, int index)
        {
            Hexagon hex = board.getHexByCoord(new HexCoord(CornerQ[index], CornerR[index], CornerS[index]));
            if (hex == null) return false;

            if (hex.type != TypeOfHex.portal) return true;
            return hex.currentHP <= 0;
        }

        // =================================================================
        //  UN SECTEUR
        // =================================================================
        private IEnumerator BloomSector(BoardController board, int index)
        {
            System.Collections.Generic.List<Hexagon> hexes = board.HexagonsInBoard;

            // Premier passage : y a-t-il seulement quelque chose a rendre ? Sans ce
            // test, un secteur deja refleuri jouerait sa ceremonie a chaque fin de
            // tour jusqu'a la fin de la partie.
            bool any = false;
            for (int i = 0; i < hexes.Count; i++)
            {
                if (!ShouldBloom(board, hexes[i], index)) continue;
                any = true;
                break;
            }
            if (!any) yield break;

            // Le bandeau se fabrique tout seul s'il n'existe pas encore : cette
            // annonce-la ne doit pas dependre d'un HUD reconstruit.
            yield return StartCoroutine(PhaseBanner.PlayPhase("bannerBloom", BannerColor));

            if (focusCamera)
            {
                Hexagon corner = board.getHexByCoord(
                    new HexCoord(CornerQ[index], CornerR[index], CornerS[index]));
                if (corner != null) CameraDirector.FocusPoint(corner.transform.position);
            }

            FXManager fx = FXManager.Instance;

            // Deuxieme passage : on rend. La liste du plateau est modifiee en cours de
            // route (UpgradeHexVisual retire l'ancienne case et ajoute la nouvelle), on
            // la reparcourt donc a l'envers - les ajouts se font a la fin.
            for (int i = hexes.Count - 1; i >= 0; i--)
            {
                Hexagon hex = (i < hexes.Count) ? hexes[i] : null;
                if (!ShouldBloom(board, hex, index)) continue;

                TypeOfHex restored = Restore(hex);
                if (restored == TypeOfHex.None) continue;

                if (fx != null) fx.SpawnBuildingUpgradeFX(hex.transform.position);

                yield return _pace.For(cellDelay);
            }

            board.RefreshAllAuras();
            board.RefreshStructureHealthBars();

            if (holdAfter > 0f) yield return _pace.For(holdAfter);

            CameraDirector.ReleaseCamera();
        }

        /// <summary>
        /// Cette case doit-elle refleurir maintenant ? Elle doit etre dans le glacis,
        /// dans le secteur de CE Shofar, encore en desert vierge, et libre de tout pion.
        /// </summary>
        private static bool ShouldBloom(BoardController board, Hexagon hex, int index)
        {
            if (hex == null || hex.positionInTheBoard == null) return false;

            // Vierge : un desert de niveau 0. Tout le reste a deja ete rendu, ou bati.
            if (hex.type != TypeOfHex.desert || hex.level != 0) return false;

            HexCoord c = hex.positionInTheBoard;

            int ring = Mathf.Max(Mathf.Abs(c.q), Mathf.Max(Mathf.Abs(c.r), Mathf.Abs(c.s)));
            if (ring < MapGenerator.GlacisRing) return false;

            if (NearestCorner(c) != index) return false;

            // Ce que le sol redeviendrait est deja ce qu'il est : rien a faire, et
            // surtout pas de ceremonie pour une case qui ne changera pas.
            if (MapGenerator.GlacisSiteAt(c.q, c.r, c.s) == TypeOfHex.None
                && MapGenerator.GroundWithoutGlacis(c.q, c.r, c.s) == TypeOfHex.desert) return false;

            // Un pion dessus : on attend le tour prochain. Une colline sous un ennemi
            // en ferait un pion coince sur une case ou personne ne marche.
            return board.getPawnByCoord(c) == null;
        }

        /// <summary>
        /// Repose sur la case ce que le glacis lui avait pris, et refait son modele.
        /// Retourne le type rendu, ou None si rien n'a change.
        /// </summary>
        private static TypeOfHex Restore(Hexagon hex)
        {
            HexCoord c = hex.positionInTheBoard;

            TypeOfHex wanted = MapGenerator.GlacisSiteAt(c.q, c.r, c.s);
            if (wanted == TypeOfHex.None) wanted = MapGenerator.GroundWithoutGlacis(c.q, c.r, c.s);
            if (wanted == hex.type) return TypeOfHex.None;

            // Le type et le niveau AVANT le modele : la fabrique lit l'hexagone pour
            // choisir son prefab, et recopie ces champs sur la nouvelle case.
            hex.type = wanted;
            hex.level = 0;
            hex.buildingType = "None";

            // Un site rendu sort de terre VIERGE : il se construit, il n'est pas offert.
            hex.maxHP = 0;
            hex.currentHP = 0;
            hex.energy = 0;
            hex.energymax = 0;

            if (BoardController.instance != null) BoardController.instance.UpgradeHexVisual(hex);

            return wanted;
        }

        /// <summary>
        /// Le Shofar dont cette case releve : le plus proche des six pointes. Sur la
        /// frontiere entre deux secteurs, l'egalite tranche pour le premier - la case
        /// refleurit donc avec le premier des deux Shofars qui tombe, ce qui est la
        /// generosite que l'on veut.
        /// </summary>
        private static int NearestCorner(HexCoord c)
        {
            int best = 0;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < 6; i++)
            {
                int dq = c.q - CornerQ[i];
                int dr = c.r - CornerR[i];
                int ds = c.s - CornerS[i];

                int distance = (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(ds)) / 2;
                if (distance < bestDistance) { bestDistance = distance; best = i; }
            }

            return best;
        }
    }
}
