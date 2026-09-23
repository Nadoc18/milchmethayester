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
    /// Les modeles 3D sont les prefabs du jeu (Resources/portal_1, base_1, enemy_2,
    /// attacker_2, desert_1, plain_1). Leurs scripts de jeu (Hexagon, PawnController,
    /// tout ce qui est MNLTHII) sont retires : dans le menu ils n'ont pas de plateau et
    /// planteraient. Les Animator et les particules sont gardes.
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

        private const string PrefabFolder = "Assets/Resources/";

        private static readonly Color ColBackground = new Color32(0x05, 0x08, 0x12, 0xFF);
        private static readonly Color ColText = new Color32(0xE6, 0xEC, 0xFF, 0xFF);
        private static readonly Color ColDim = new Color32(0x9A, 0xA6, 0xC8, 0xFF);
        private static readonly Color ColGold = new Color32(0xFF, 0xC6, 0x5C, 0xFF);
        private static readonly Color ColCyan = new Color32(0x3D, 0xE0, 0xD0, 0xFF);
        private static readonly Color ColHexFill = new Color32(0x0A, 0x10, 0x22, 0xE6);

        // --- le plateau d'interface ---
        /// <summary>Cote du carre de chaque image hexagonale, en pixels de reference.</summary>
        private const float HexSize = 190f;
        /// <summary>Largeur reelle de l'hexagone dans son image (pointe en haut) : 886 / 1024.</summary>
        private const float HexWidthRatio = 0.8652f;
        /// <summary>Petit jour entre deux cases.</summary>
        private const float HexGap = 1.06f;
        private static readonly Vector2 BoardCenter = new Vector2(0f, -40f);

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

            Sprite hexFilled = LoadSprite(HexFilledPath);
            Sprite hexOutline = LoadSprite(HexOutlinePath);
            Sprite hexDash = LoadSprite(HexDashPath);
            if (hexDash == null) hexDash = hexOutline;

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

            // =============================================================
            //  LA SCENE 3D : deux ilots qui se font face
            // =============================================================
            GameObject world = new GameObject("Diorama");

            Vector3 portalPos = new Vector3(-IslandX, 0f, 0f);
            Vector3 basePos = new Vector3(IslandX, 0f, 0f);

            BuildIsland(world.transform, "Ilot_Shofar", portalPos, "portal_1", new[] { "desert_1", "desert_2", "plain_1" });
            BuildIsland(world.transform, "Ilot_Base", basePos, "base_1", new[] { "plain_1", "plain_2", "desert_1" });

            GameObject enemy = PlaceModel(world.transform, "enemy_2", "Ennemi_Rang2", portalPos);
            GameObject tank = PlaceModel(world.transform, "attacker_2", "Tank_Rang2", basePos);

            // Ils se regardent.
            if (enemy != null) FaceTowards(enemy.transform, basePos);
            if (tank != null) FaceTowards(tank.transform, portalPos);

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

            MainMenuController ctrl = canvasGo.AddComponent<MainMenuController>();
            ctrl.idleColor = ColHexFill;
            ctrl.selectedColor = new Color(1f, 1f, 1f, 0.30f);
            MainMenuDiorama diorama = canvasGo.AddComponent<MainMenuDiorama>();
            diorama.cameraRig = camGo.transform;
            diorama.focus = focus;

            // --- titre ---
            TextMeshProUGUI title = NewText("Titre", root, HudBuilder.S(mm, "menuTitle"), 104f, ColGold, true);
            Place(title.rectTransform, new Vector2(0f, 440f), new Vector2(1500f, 140f));
            title.fontStyle = FontStyles.Bold;
            ctrl.titleText = title;

            TextMeshProUGUI subtitle = NewText("SousTitre", root, HudBuilder.S(mm, "menuSubtitle"), 38f, ColDim, true);
            Place(subtitle.rectTransform, new Vector2(0f, 350f), new Vector2(1200f, 60f));
            ctrl.subtitleText = subtitle;

            // =============================================================
            //  LE PLATEAU D'HEXAGONES
            // =============================================================
            RectTransform board = NewRect("Plateau", root);
            Place(board, BoardCenter, new Vector2(10f, 10f));

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

            // Nid d'abeille de rayon 2 (19 cases), pointes en haut, coordonnees axiales.
            for (int r = -2; r <= 2; r++)
            {
                for (int q = -2; q <= 2; q++)
                {
                    int s = -q - r;
                    if (s < -2 || s > 2) continue;

                    Vector2 pos = HexToPixel(q, r);
                    string cellName = "Case_" + q + "_" + r;

                    // Rangee du haut : les trois difficultes. En hebreu on lit de
                    // droite a gauche : Facile (index 0) a DROITE.
                    int difficulty = -1;
                    if (r == -2)
                    {
                        // q = 0, 1, 2 donnent x = -1, 0, +1 largeur.
                        if (q == 2) difficulty = 0;
                        else if (q == 1) difficulty = 1;
                        else if (q == 0) difficulty = 2;
                    }

                    if (difficulty >= 0)
                    {
                        Button button; Image fill; Image frame; TextMeshProUGUI label;
                        BuildHexButton(board, cellName, pos, hexFilled, hexOutline,
                                       HudBuilder.S(mm, nameKeys[difficulty]), 40f, ColText,
                                       out button, out fill, out frame, out label);

                        ctrl.difficultyButtons[difficulty] = button;
                        ctrl.difficultyBackgrounds[difficulty] = fill;
                        ctrl.difficultyFrames[difficulty] = frame;
                        ctrl.difficultyNames[difficulty] = label;
                        continue;
                    }

                    if (q == 0 && r == 0)
                    {
                        Button button; Image fill; Image frame; TextMeshProUGUI label;
                        BuildHexButton(board, "NouvellePartie", pos, hexFilled, hexOutline,
                                       HudBuilder.S(mm, "menuNewGame"), 42f, ColGold,
                                       out button, out fill, out frame, out label);

                        fill.color = new Color(ColGold.r, ColGold.g, ColGold.b, 0.22f);
                        frame.color = ColGold;
                        label.enableWordWrapping = true;

                        // Le bouton principal un peu plus grand que les autres.
                        button.transform.localScale = Vector3.one;
                        ((RectTransform)button.transform).sizeDelta = new Vector2(HexSize * 1.08f, HexSize * 1.08f);

                        ctrl.newGameButton = button;
                        ctrl.newGameText = label;
                        continue;
                    }

                    // --- case decorative ---
                    RectTransform cell = NewRect(cellName, board);
                    Place(cell, pos, new Vector2(HexSize, HexSize));

                    Image back = NewHexImage("Fond", cell, hexFilled, ColHexFill);
                    back.raycastTarget = false;

                    // Anneau interieur (tirets) sur une case sur deux, contour plein ailleurs.
                    bool dashed = ((q + 2 * r) & 1) == 0;
                    float alpha = dashed ? 0.30f : 0.45f;
                    Image edge = NewHexImage("Contour", cell, dashed ? hexDash : hexOutline,
                                             new Color(ColCyan.r, ColCyan.g, ColCyan.b, alpha));
                    edge.raycastTarget = false;

                    decor.Add(edge);
                    decorAlpha.Add(alpha);
                    decorPos.Add(pos);
                }
            }

            diorama.decor = decor.ToArray();
            diorama.decorAlpha = decorAlpha.ToArray();
            diorama.decorPosition = decorPos.ToArray();

            // --- la description de la difficulte choisie ---
            TextMeshProUGUI body = NewText("Description", root, "", 32f, ColDim, true);
            Place(body.rectTransform, new Vector2(0f, BoardCenter.y - 2f * 0.75f * HexSize * HexGap - HexSize * 0.62f),
                  new Vector2(1100f, 60f));
            ctrl.selectedBodyText = body;

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
        /// <summary>Hexagone pointe en haut, coordonnees axiales -> pixels.</summary>
        private static Vector2 HexToPixel(int q, int r)
        {
            float w = HexSize * HexWidthRatio * HexGap;
            float h = HexSize * 0.75f * HexGap;
            return new Vector2(w * (q + r * 0.5f), -h * r);
        }

        private static void BuildHexButton(RectTransform parent, string name, Vector2 pos,
                                           Sprite filled, Sprite outline, string text, float fontSize, Color textColor,
                                           out Button button, out Image fill, out Image frame, out TextMeshProUGUI label)
        {
            RectTransform cell = NewRect(name, parent);
            Place(cell, pos, new Vector2(HexSize, HexSize));

            fill = NewHexImage("Fond", cell, filled, ColHexFill);
            fill.raycastTarget = false;

            frame = NewHexImage("Contour", cell, outline, new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.6f));
            frame.raycastTarget = false;

            // Zone cliquable : un rectangle INSCRIT dans l'hexagone. L'image carree du
            // contour deborde sur les voisines ; sans cela, un clic dans un coin
            // pouvait tomber sur la case d'a cote.
            Image hit = NewImage("ZoneClic", cell, new Color(1f, 1f, 1f, 0f));
            RectTransform hitRect = hit.rectTransform;
            hitRect.anchorMin = new Vector2(0.5f, 0.5f);
            hitRect.anchorMax = new Vector2(0.5f, 0.5f);
            hitRect.sizeDelta = new Vector2(HexSize * HexWidthRatio * 0.96f, HexSize * 0.72f);
            hit.raycastTarget = true;

            button = cell.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;

            cell.gameObject.AddComponent<MainMenuHexButton>();

            label = NewText("Libelle", cell, text, fontSize, textColor, true);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = new Vector2(HexSize * HexWidthRatio * 0.86f, HexSize * 0.6f);
            label.fontStyle = FontStyles.Bold;
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
            if (prefab == null)
            {
                Debug.LogWarningFormat("[MainMenuBuilder] Prefab introuvable : {0}", path);
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
