using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;
using Newtonsoft.Json.Linq;
using MNLTHII.Managers;

namespace MNLTHII.EditorTools
{
    /// <summary>
    /// Construit la scene du MENU PRINCIPAL, et la met en premier dans le build.
    ///
    /// Menu : Milchemet > Construire le menu principal
    ///
    /// LA SCENE
    ///   - En 3D, deux petits ilots d'hexagones du jeu : a gauche un Shofar (portail)
    ///     avec un ennemi de rang 2 dessus, a droite une case de Base avec un Tank de
    ///     rang 2. Ils se font face. La camera respire lentement (MainMenuDiorama).
    ///   - Au centre, l'interface : un PLATEAU D'HEXAGONES fait avec les textures
    ///     Hexagon de Reach UI (Textures/Borders/Hexagon). Dix-neuf cases en nid
    ///     d'abeille ; la rangee du haut porte les trois difficultes (Facile a droite,
    ///     sens hebreu), la case du centre est "Nouvelle partie", les autres sont
    ///     decoratives et pulsent doucement.
    ///   - Le titre au-dessus, la description de la difficulte choisie en dessous.
    ///
    /// Les modeles 3D sont les prefabs du jeu (portal_1, base_1, enemy_2, attacker_2,
    /// desert_1, plain_1 - dans Art/Board, ou Resources s'ils n'ont pas bouge). Leurs
    /// scripts de jeu (Hexagon, PawnController, tout ce qui est MNLTHII) sont retires :
    /// dans le menu ils n'ont pas de plateau et planteraient. Les Animator et les
    /// particules sont gardes.
    ///
    /// Relancable sans risque : la scene du menu est simplement reecrite. La scene de
    /// jeu n'est jamais modifiee. Pour deplacer un modele ou la camera, fais-le a la
    /// main dans la scene MainMenu apres construction (ne relance pas ensuite, ou note
    /// tes valeurs).
    ///
    /// Les libelles viennent de hud_labels.json, section mainMenu. Le .cs reste en
    /// pur ASCII.
    /// </summary>
    public static class MainMenuBuilder
    {
        private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string GameScenePath = "Assets/Scenes/Game.unity";

        private const string HexFolder = "Assets/Reach - Complete Sci-Fi UI/Textures/Borders/Hexagon/64x/";
        private const string HexFilledPath = HexFolder + "Hexagon Filled 64x.png";
        private const string HexOutlinePath = HexFolder + "Hexagon Outline 64px - 2x.png";
        private const string HexDashPath = HexFolder + "Hexagon Dash 64px - 3x.png";

        private const string PrefabFolder = "Assets/Art/Board/";
        private const string LegacyPrefabFolder = "Assets/Resources/";

        // LES IMAGES DE DAN. Le menu est fait avec elles et rien d'autre : le fond, le
        // logo complet (plaque, circuits, badges et titre bicolore), et les cinq
        // hexagones (normal, selectionne, principal, deco rouge, deco cyan).
        private const string ArtFolder = "Assets/Art/Sprites/";
        private const string ArtBackground = ArtFolder + "00_fond_menu_1280x720.png";
        private const string ArtLogo = ArtFolder + "logo_milchemet_hayetzer.png";
        private const string ArtHexNormal = ArtFolder + "04_hexagone_normal.png";
        private const string ArtHexSelected = ArtFolder + "05_hexagone_selectionne.png";
        private const string ArtHexMain = ArtFolder + "06_hexagone_principal.png";
        private const string ArtHexDecoRed = ArtFolder + "07_hexagone_deco_rouge.png";
        private const string ArtHexDecoCyan = ArtFolder + "08_hexagone_deco_cyan.png";

        private static readonly Color ColBackground = new Color32(0x05, 0x08, 0x12, 0xFF);
        private static readonly Color ColText = new Color32(0xE6, 0xEC, 0xFF, 0xFF);
        private static readonly Color ColDim = new Color32(0x9A, 0xA6, 0xC8, 0xFF);
        private static readonly Color ColGold = new Color32(0xFF, 0xC6, 0x5C, 0xFF);
        private static readonly Color ColCyan = new Color32(0x3D, 0xE0, 0xD0, 0xFF);
        private static readonly Color ColHexFill = new Color32(0x0A, 0x10, 0x22, 0xE6);

        // --- le plateau d'interface : les cotes de la maquette (1280 x 720) x 1.5 ---
        /// <summary>Largeur d'un hexagone normal a l'ecran (image 232 x 268, pointe en haut).</summary>
        private const float HexW = 200f;
        private const float HexH = HexW * 268f / 232f;
        /// <summary>L'hexagone selectionne (312 px) et le principal (360 px) sont dessines
        /// au meme rapport que dans leurs images : leur corps tombe exactement sur le normal.</summary>
        private const float SelectedScale = 312f / 232f;
        private const float MainScale = 360f / 232f;
        /// <summary>D'un centre au suivant sur une rangee, et d'une rangee a la suivante.</summary>
        private const float ColStep = 206f;
        private const float RowStep = 164f;
        private static readonly Vector2 BoardCenter = new Vector2(0f, -70f);

        // --- la scene 3D ---
        /// <summary>Pas entre deux cases du jeu (0.55 * racine de 3).</summary>
        private const float WorldHex = 0.9526f;
        private const float IslandX = 3.9f;

        private static TMP_FontAsset _font;

        [MenuItem("Milchemet/Construire le menu principal", false, 11)]
        public static void Build()
        {
            // Rien n'est perdu : la scene ouverte est enregistree (si on le veut) avant
            // qu'on en cree une nouvelle.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            JObject labels = HudBuilder.LoadLabels();
            if (labels == null) return;
            JObject mm = labels["mainMenu"] as JObject;
            if (mm == null)
                Debug.LogWarning("[MainMenuBuilder] Section mainMenu absente de hud_labels.json : textes vides.");

            _font = HudBuilder.FindHebrewFont();

            EnsureArtSprites();

            Sprite hexFilled = LoadSprite(HexFilledPath);
            Sprite hexOutline = LoadSprite(HexOutlinePath);

            // L'inspecteur affichait peut-etre un objet de la scene qu'on va fermer :
            // on le vide AVANT, sinon Unity se plaint d'objets disparus (erreurs
            // d'inspecteur sans consequence, mais inquietantes).
            Selection.activeObject = null;
            ActiveEditorTracker.sharedTracker.ForceRebuild();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // =============================================================
            //  LUMIERE ET CAMERA
            // =============================================================
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.36f, 0.42f, 0.58f);
            RenderSettings.ambientEquatorColor = new Color(0.20f, 0.22f, 0.30f);
            RenderSettings.ambientGroundColor = new Color(0.08f, 0.08f, 0.12f);

            GameObject sun = new GameObject("Soleil", typeof(Light));
            Light sunLight = sun.GetComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.intensity = 1.15f;
            sunLight.color = new Color(1f, 0.95f, 0.86f);
            sunLight.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            GameObject rim = new GameObject("Contre-jour", typeof(Light));
            Light rimLight = rim.GetComponent<Light>();
            rimLight.type = LightType.Directional;
            rimLight.intensity = 0.55f;
            rimLight.color = new Color(0.45f, 0.65f, 1f);
            rimLight.shadows = LightShadows.None;
            rim.transform.rotation = Quaternion.Euler(20f, 160f, 0f);

            Vector3 focus = new Vector3(0f, 0.55f, 0f);

            GameObject camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            Camera cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = ColBackground;
            cam.orthographic = false;
            cam.fieldOfView = 36f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            camGo.transform.position = focus + new Vector3(0f, 2.7f, -9.2f);
            camGo.transform.LookAt(focus);

            // Pas de diorama 3D : le fond est l'image de Dan, plein ecran.

            // =============================================================
            //  EVENTSYSTEM ET CANVAS
            // =============================================================
            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));

            GameObject canvasGo = new GameObject("Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = NewRect("Menu", canvasGo.transform);
            Stretch(root);

            // --- le fond ---
            Sprite background = LoadSprite(ArtBackground);
            Image bg = NewImage("Fond", root, Color.white);
            Stretch(bg.rectTransform);
            bg.raycastTarget = false;
            if (background != null) { bg.sprite = background; bg.type = Image.Type.Simple; bg.preserveAspect = false; }
            else bg.color = ColBackground;

            MainMenuController ctrl = canvasGo.AddComponent<MainMenuController>();
            // Les hexagones sont des images deja colorees : le controleur ne les teinte
            // plus (blanc = telles quelles). Il allume seulement l'hexagone selectionne
            // ("Contour") de la difficulte choisie et eteint les autres.
            ctrl.idleColor = Color.white;
            ctrl.selectedColor = Color.white;
            ctrl.frameIdleAlpha = 0f;
            ctrl.accentColors = new Color[] { Color.white, Color.white, Color.white };
            ctrl.nightVeil = new Color(0f, 0f, 0f, 0f);
            ctrl.titleSpriteResource = "";

            MainMenuDiorama diorama = canvasGo.AddComponent<MainMenuDiorama>();
            diorama.cameraRig = null;
            diorama.focus = focus;

            // --- le logo : l'image complete de Dan, plaque et titre compris ---
            Sprite logo = LoadSprite(ArtLogo);
            Image logoImage = NewImage("Logo", root, Color.white);
            logoImage.raycastTarget = false;
            logoImage.preserveAspect = true;
            if (logo != null) logoImage.sprite = logo;
            // 2650 x 807 : a 980 de large, 298 de haut.
            Place(logoImage.rectTransform, new Vector2(0f, 352f), new Vector2(980f, 298f));
            ctrl.titleImage = logoImage;

            // Le titre ecrit reste dans la scene, eteint : le controleur en a besoin
            // comme repere, et il sert de secours si l'image venait a manquer.
            TextMeshProUGUI title = NewText("Titre", root, HudBuilder.S(mm, "menuTitle"), 96f, ColGold, true);
            Place(title.rectTransform, new Vector2(0f, 352f), new Vector2(1500f, 140f));
            title.fontStyle = FontStyles.Bold;
            title.gameObject.SetActive(logo == null);
            ctrl.titleText = title;

            TextMeshProUGUI subtitle = NewText("SousTitre", root, HudBuilder.S(mm, "menuSubtitle"), 30f, ColDim, true);
            Place(subtitle.rectTransform, new Vector2(0f, 186f), new Vector2(1200f, 50f));
            ctrl.subtitleText = subtitle;

            // =============================================================
            //  LE PLATEAU : sept hexagones, comme sur la maquette
            //
            //      [ difficile ] [ normal ] [ facile ]         (facile a DROITE : hebreu)
            //           [ comment jouer ] [ reprendre ]
            //      ( rouge )   [ NOUVELLE PARTIE ]   ( cyan )  (deux cases decoratives)
            // =============================================================
            RectTransform board = NewRect("Plateau", root);
            Place(board, BoardCenter, new Vector2(10f, 10f));

            Sprite hexNormal = LoadSprite(ArtHexNormal);
            Sprite hexSelected = LoadSprite(ArtHexSelected);
            Sprite hexMain = LoadSprite(ArtHexMain);
            Sprite hexDecoRed = LoadSprite(ArtHexDecoRed);
            Sprite hexDecoCyan = LoadSprite(ArtHexDecoCyan);
            if (hexNormal == null) hexNormal = hexFilled;
            if (hexSelected == null) hexSelected = hexOutline;
            if (hexMain == null) hexMain = hexNormal;

            string[] nameKeys = { "menuEasy", "menuMedium", "menuHard" };
            string[] bodyKeys = { "menuEasyBody", "menuMediumBody", "menuHardBody" };

            ctrl.difficultyButtons = new Button[3];
            ctrl.difficultyBackgrounds = new Image[3];
            ctrl.difficultyOutlines = new UnityEngine.UI.Outline[0];
            ctrl.difficultyNames = new TextMeshProUGUI[3];
            ctrl.difficultyBodies = new TextMeshProUGUI[0];
            ctrl.difficultyFrames = new Image[3];
            ctrl.bodyLabels = new string[3];
            for (int i = 0; i < 3; i++) ctrl.bodyLabels[i] = HudBuilder.S(mm, bodyKeys[i]);

            List<Image> decor = new List<Image>();
            List<float> decorAlpha = new List<float>();
            List<Vector2> decorPos = new List<Vector2>();

            // Rangee du haut : les trois difficultes. Facile (index 0) a DROITE.
            for (int d = 0; d < 3; d++)
            {
                Vector2 pos = new Vector2((1 - d) * ColStep, RowStep);
                Button button; Image fill; Image frame; TextMeshProUGUI label;
                BuildHexButton(board, "Difficulte" + d, pos, hexNormal, hexSelected, SelectedScale,
                               HudBuilder.S(mm, nameKeys[d]), 38f, ColText,
                               out button, out fill, out frame, out label);
                ctrl.difficultyButtons[d] = button;
                ctrl.difficultyBackgrounds[d] = fill;
                ctrl.difficultyFrames[d] = frame;
                ctrl.difficultyNames[d] = label;
            }

            // Rangee du milieu : comment jouer (a gauche), reprendre (a droite).
            Button helpButton;
            {
                Button button; Image fill; Image frame; TextMeshProUGUI label;
                BuildHexButton(board, "CommentJouer", new Vector2(-ColStep * 0.5f, 0f), hexNormal, hexSelected, SelectedScale,
                               ReadHelpLabel(), 30f, ColText, out button, out fill, out frame, out label);
                label.enableWordWrapping = true;
                frame.gameObject.SetActive(false);
                helpButton = button;
            }
            {
                Button button; Image fill; Image frame; TextMeshProUGUI label;
                BuildHexButton(board, "Reprendre", new Vector2(ColStep * 0.5f, 0f), hexNormal, hexSelected, SelectedScale,
                               HudBuilder.S(mm, "menuLoadGame"), 30f, ColText, out button, out fill, out frame, out label);
                label.enableWordWrapping = true;
                frame.gameObject.SetActive(false);
                ctrl.loadGameButton = button;
                ctrl.loadGameText = label;
            }

            // Rangee du bas : la nouvelle partie au centre (l'hexagone principal, plein
            // et lumineux), entre deux cases decoratives : rouge a gauche (le Yetzer),
            // cyan a droite (nous).
            {
                Button button; Image fill; Image frame; TextMeshProUGUI label;
                BuildHexButton(board, "NouvellePartie", new Vector2(0f, -RowStep), hexMain, hexSelected, SelectedScale,
                               HudBuilder.S(mm, "menuNewGame"), 38f, ColText, out button, out fill, out frame, out label);
                // Le principal est dessine au rapport de son image (360 / 232).
                fill.rectTransform.sizeDelta = new Vector2(HexW * MainScale, HexH * MainScale);
                label.enableWordWrapping = true;
                frame.gameObject.SetActive(false);
                ctrl.newGameButton = button;
                ctrl.newGameText = label;
            }
            {
                RectTransform cell = NewRect("DecorRouge", board);
                Place(cell, new Vector2(-ColStep, -RowStep), new Vector2(HexW, HexH));
                Image edge = NewHexImage("Contour", cell, hexDecoRed != null ? hexDecoRed : hexNormal, Color.white);
                edge.raycastTarget = false;
                decor.Add(edge); decorAlpha.Add(1f); decorPos.Add(new Vector2(-ColStep, -RowStep));
            }
            {
                RectTransform cell = NewRect("DecorCyan", board);
                Place(cell, new Vector2(ColStep, -RowStep), new Vector2(HexW, HexH));
                Image edge = NewHexImage("Contour", cell, hexDecoCyan != null ? hexDecoCyan : hexNormal, Color.white);
                edge.raycastTarget = false;
                decor.Add(edge); decorAlpha.Add(1f); decorPos.Add(new Vector2(ColStep, -RowStep));
            }

            diorama.decor = decor.ToArray();
            diorama.decorAlpha = decorAlpha.ToArray();
            diorama.decorPosition = decorPos.ToArray();

            // --- la description de la difficulte choisie, sous le plateau ---
            TextMeshProUGUI body = NewText("Description", root, "", 28f, ColDim, true);
            Place(body.rectTransform, new Vector2(0f, BoardCenter.y - RowStep - HexH * 0.5f - 40f), new Vector2(1200f, 50f));
            ctrl.selectedBodyText = body;

            // --- la page "comment on joue", la meme que dans le jeu ---
            HowToPlayPanel help = canvasGo.AddComponent<HowToPlayPanel>();
            help.openButton = helpButton;
            HudBuilder.BuildHelpPageInto(root, canvas, help);
            HudBuilder.EnsureTeachingManagerIn(canvasGo);

            // =============================================================
            //  ENREGISTREMENT
            // =============================================================
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, MenuScenePath))
            {
                Debug.LogErrorFormat("[MainMenuBuilder] Impossible d'enregistrer {0}.", MenuScenePath);
                return;
            }

            RegisterInBuild();

            Selection.activeGameObject = canvasGo;

            Debug.Log("[MainMenuBuilder] Menu principal construit (" + MenuScenePath + ") et place en premier "
                    + "dans le build. Pour revenir au jeu : ouvre Assets/Scenes/Game.unity.");
        }

        // =================================================================
        //  PLATEAU D'INTERFACE
        // =================================================================
        private static void BuildHexButton(RectTransform parent, string name, Vector2 pos,
                                           Sprite filled, Sprite outline, float outlineScale,
                                           string text, float fontSize, Color textColor,
                                           out Button button, out Image fill, out Image frame, out TextMeshProUGUI label)
        {
            RectTransform cell = NewRect(name, parent);
            Place(cell, pos, new Vector2(HexW, HexH));

            // Le fond est l'image telle quelle (blanc = pas de teinte).
            fill = NewHexImage("Fond", cell, filled, Color.white);
            fill.raycastTarget = false;

            // Le "contour" est l'hexagone selectionne, pose par-dessus a l'echelle de
            // son image (son halo deborde du corps) : le controleur l'allume sur la
            // difficulte choisie, l'eteint sur les autres.
            frame = NewHexImage("Contour", cell, outline, Color.white);
            frame.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            frame.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            frame.rectTransform.sizeDelta = new Vector2(HexW * outlineScale, HexH * outlineScale);
            frame.raycastTarget = false;

            // Zone cliquable : un rectangle INSCRIT dans l'hexagone. L'image carree
            // deborde sur les voisines ; sans cela, un clic dans un coin pouvait
            // tomber sur la case d'a cote.
            Image hit = NewImage("ZoneClic", cell, new Color(1f, 1f, 1f, 0f));
            RectTransform hitRect = hit.rectTransform;
            hitRect.anchorMin = new Vector2(0.5f, 0.5f);
            hitRect.anchorMax = new Vector2(0.5f, 0.5f);
            hitRect.sizeDelta = new Vector2(HexW * 0.96f, HexH * 0.72f);
            hit.raycastTarget = true;

            button = cell.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            button.colors = colors;

            cell.gameObject.AddComponent<MainMenuHexButton>();

            label = NewText("Libelle", cell, text, fontSize, textColor, true);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = new Vector2(HexW * 0.82f, HexH * 0.6f);
            label.fontStyle = FontStyles.Bold;
        }

        /// <summary>Le libelle du bouton d'aide, lu dans teachings.json comme dans le jeu.</summary>
        private static string ReadHelpLabel()
        {
            string label = HudBuilder.ReadTeachingLabelPublic("helpButton");
            return string.IsNullOrEmpty(label) ? "?" : label;
        }

        private static Image NewHexImage(string name, RectTransform parent, Sprite sprite, Color color)
        {
            Image img = NewImage(name, parent, color);
            Stretch(img.rectTransform);
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            return img;
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            RectTransform rect = NewRect(name, parent);
            Image img = rect.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        /// <summary>
        /// Les images de Dan, importees en Sprite (sans mipmaps, non compressees : ce
        /// sont des elements d'interface, ils doivent rester nets).
        /// </summary>
        private static void EnsureArtSprites()
        {
            string[] files = { ArtBackground, ArtLogo, ArtHexNormal, ArtHexSelected, ArtHexMain, ArtHexDecoRed, ArtHexDecoCyan };
            for (int i = 0; i < files.Length; i++)
            {
                TextureImporter importer = AssetImporter.GetAtPath(files[i]) as TextureImporter;
                if (importer == null) { Debug.LogWarningFormat("[MainMenuBuilder] Image introuvable : {0}", files[i]); continue; }

                bool dirty = false;
                if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
                if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
                if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
                if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }
                if (importer.maxTextureSize < 2048) { importer.maxTextureSize = 2048; dirty = true; }
                if (dirty) importer.SaveAndReimport();
            }
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                Debug.LogWarningFormat("[MainMenuBuilder] Texture hexagonale introuvable (ou pas en Sprite) : {0}", path);
            return sprite;
        }

        // =================================================================
        //  SCENE 3D
        // =================================================================
        /// <summary>Une case centrale et ses six voisines, prises dans les prefabs du jeu.</summary>
        private static void BuildIsland(Transform parent, string name, Vector3 center, string centerPrefab, string[] ring)
        {
            GameObject island = new GameObject(name);
            island.transform.SetParent(parent, false);
            island.transform.position = center;

            PlaceModel(island.transform, centerPrefab, "Centre", center);

            for (int i = 0; i < 6; i++)
            {
                // Meme grille que le jeu (PawnFactory) : le voisin q+1 est a +X, les
                // autres tous les 60 degres.
                float angle = (60f * i) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * WorldHex;
                string prefab = ring[i % ring.Length];
                PlaceModel(island.transform, prefab, "Voisin" + i, center + offset);
            }
        }

        /// <summary>
        /// Pose un prefab du jeu, sans ses scripts de jeu. Null (et un avertissement)
        /// si le prefab n'existe pas : le reste du menu se construit quand meme.
        /// </summary>
        private static GameObject PlaceModel(Transform parent, string prefabName, string objectName, Vector3 position)
        {
            string path = PrefabFolder + prefabName + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            // Les prefabs n'ont pas encore bouge : l'ancien dossier.
            if (prefab == null)
            {
                path = LegacyPrefabFolder + prefabName + ".prefab";
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            if (prefab == null)
            {
                Debug.LogWarningFormat("[MainMenuBuilder] Prefab introuvable : {0}{1}.prefab", PrefabFolder, prefabName);
                return null;
            }

            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (go == null) return null;

            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            go.name = objectName;
            go.transform.SetParent(parent, true);
            go.transform.position = position;

            StripGameplay(go);
            return go;
        }

        /// <summary>
        /// Retire ce qui a besoin du plateau de jeu : les scripts du projet (Hexagon,
        /// PawnController, tout MNLTHII), les petits canevas d'information, et coupe les
        /// sons. Les Animator, les particules et les scripts des assets restent.
        /// </summary>
        private static void StripGameplay(GameObject go)
        {
            Canvas[] canvases = go.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
                if (canvases[i] != null) Object.DestroyImmediate(canvases[i].gameObject);

            // Deux passes : un script peut en exiger un autre (RequireComponent).
            for (int pass = 0; pass < 2; pass++)
            {
                MonoBehaviour[] scripts = go.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < scripts.Length; i++)
                {
                    MonoBehaviour mb = scripts[i];
                    if (mb == null) continue;

                    System.Type t = mb.GetType();
                    bool gameplay = t == typeof(Hexagon) || t == typeof(PawnController)
                                    || (t.Namespace != null && t.Namespace.StartsWith("MNLTHII"));
                    if (gameplay) Object.DestroyImmediate(mb);
                }
            }

            AudioSource[] sources = go.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] == null) continue;
                sources[i].playOnAwake = false;
                sources[i].enabled = false;
            }
        }

        private static void FaceTowards(Transform t, Vector3 target)
        {
            Vector3 dir = target - t.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) t.rotation = Quaternion.LookRotation(dir);
        }

        // =================================================================
        //  BUILD
        // =================================================================
        /// <summary>MainMenu en premier, Game ensuite, les autres scenes gardees derriere.</summary>
        private static void RegisterInBuild()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            scenes.RemoveAll(s => s.path == MenuScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(MenuScenePath, true));

            bool hasGame = false;
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path != GameScenePath) continue;
                hasGame = true;
                scenes[i].enabled = true;
            }
            if (!hasGame) scenes.Insert(1, new EditorBuildSettingsScene(GameScenePath, true));

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // =================================================================
        //  OUTILS
        // =================================================================
        private static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Centre ancre au milieu du parent, position et taille en pixels de reference.</summary>
        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, string content,
                                               float size, Color color, bool rightToLeft)
        {
            RectTransform rect = NewRect(name, parent);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();

            if (_font != null) text.font = _font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.isRightToLeftText = rightToLeft;
            text.raycastTarget = false;
            text.enableWordWrapping = false;

            HudBuilder.DisableFontFeatures(text);
            return text;
        }
    }
}
