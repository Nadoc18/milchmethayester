using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using Newtonsoft.Json.Linq;
using MNLTHII.Rules;
using MNLTHII.Managers;

namespace MNLTHII.EditorTools
{
    /// <summary>
    /// Construit toute la hierarchie du HUD en un coup, et la cable.
    ///
    /// Menu : Milchemet > Construire le HUD
    ///
    /// Ce qu'il fait :
    ///   - cree (ou reutilise) un Canvas correctement regle ;
    ///   - pose le bandeau permanent, en miroir pour l'hebreu : energie a droite,
    ///     tour et rail au centre, objectifs a gauche, actions en bas ;
    ///   - pose le panneau d'etat d'un Shofar et l'info-bulle de cout ;
    ///   - pose le panneau de question (phase 1) et la carte d'unite, commune aux
    ///     phases 3 et 4 ;
    ///   - ajoute HudController, PortalPanelController, HexTooltipController,
    ///     TriviaPanelController et UnitActionCard, et glisse TOUTES les
    ///     references a ta place - y compris le lien vers le TriviaManager ;
    ///   - lit les libelles hebreux dans Assets/Resources/hud_labels.json ;
    ///   - met Font Features sur "Nothing" sur chaque texte : le crenage de TMP
    ///     est concu pour un flux gauche-droite et deforme l'hebreu.
    ///
    /// L'HEBREU NE VIT QUE DANS LE JSON. Un fichier .cs qui contient de l'hebreu
    /// finit un jour re-enregistre dans un autre encodage et le projet ne compile
    /// plus : c'est exactement ce qui est arrive a GameCanvasController.cs, reste
    /// en Windows-1252. Ici le .cs est en pur ASCII et le texte arrive du JSON.
    ///
    /// Relancable sans risque : l'ancien HUD est supprime et reconstruit. Modifie
    /// le JSON, relance, et tes libelles sont a jour.
    /// </summary>
    public static class HudBuilder
    {
        private const string RootName = "HUD";
        private const string CanvasName = "HUD Canvas";
        private const int SortingOrder = 5;
        private const string LabelsPath = "Assets/Resources/hud_labels.json";

        // Reference 1920 x 1080. Les valeurs des maquettes (1440 x 900) sont
        // transposees au prorata.
        private const float RefWidth = 1920f;
        private const float RefHeight = 1080f;

        // --- palette, identique aux maquettes ---
        //
        // LE STYLE : bleu nuit, bords fins lumineux, coins chanfreines. Deux couleurs
        // portent du sens et deux seulement : le CYAN pour ce qui est a nous (la Base,
        // l'Energie, le bouton principal), le ROUGE pour ce qui menace (les Shofars, le
        // Yetzer Hara, la menace). L'acier (blanc bleute) habille tout le reste. L'or
        // ne sert plus qu'aux mises en avant rares (recompenses, enseignement).
        private static readonly Color ColText = new Color32(0xF2, 0xF6, 0xFF, 0xFF);
        private static readonly Color ColDim = new Color32(0xA9, 0xB8, 0xD6, 0xFF);
        private static readonly Color ColGold = new Color32(0xFF, 0xC6, 0x5C, 0xFF);
        private static readonly Color ColCyan = new Color32(0x5B, 0xC8, 0xF5, 0xFF);
        private static readonly Color ColOrange = new Color32(0xFF, 0x5C, 0x6C, 0xFF);
        private static readonly Color ColRed = new Color32(0xFF, 0x5C, 0x6C, 0xFF);
        private static readonly Color ColSteel = new Color32(0xC9, 0xD6, 0xF2, 0xFF);
        private static readonly Color ColPanel = new Color32(0x0A, 0x0F, 0x1C, 0xE0);
        private static readonly Color ColLine = new Color32(0x7E, 0xA0, 0xFF, 0x24);
        private static readonly Color ColIdle = new Color32(0x6E, 0x7C, 0x9C, 0xFF);

        /// <summary>Hauteur du bandeau photo de l'info-bulle.</summary>
        private const float TooltipPhotoHeight = 230f;

        /// <summary>Hauteur de la rangee des evolutions (vignettes + libelles).</summary>
        private const float TooltipEvolutionHeight = 164f;

        // =================================================================
        //  LISIBILITE
        // =================================================================
        /// <summary>
        /// Multiplicateur applique a TOUTES les tailles de texte du HUD.
        ///
        /// Les tailles d'origine ont ete posees pour un ecran de bureau a 1920 de
        /// large. Sur un telephone, le meme rapport donne des libelles qu'on ne lit
        /// pas : la surface est physiquement dix fois plus petite, et l'oeil est a
        /// trente centimetres, pas a soixante.
        ///
        /// Une molette unique plutot que soixante nombres a retoucher : une valeur a
        /// changer ici, tout suit, et rien ne peut se desaccorder au passage.
        /// </summary>
        private const float TextScale = 1.5f;

        /// <summary>
        /// Plancher absolu, applique APRES le multiplicateur.
        ///
        /// Les plus petits libelles - les notes de bas de panneau, les unites - etaient
        /// a 12 ou 13. Multiplies par 1.35 ils restent sous 18, et sous 18 un texte
        /// n'est plus lu sur telephone : il est devine. Mieux vaut une note un peu
        /// grosse qu'une note decorative.
        /// </summary>
        private const float MinFontSize = 24f;

        private static TMP_FontAsset _font;

        [MenuItem("Milchemet/Construire le HUD", false, 10)]
        public static void Build()
        {
            JObject labels = LoadLabels();
            if (labels == null) return;

            _font = FindHebrewFont();

            Canvas canvas = EnsureCanvas();
            if (canvas == null) return;

            // Reconstruction propre : on efface l'ancien HUD s'il existe.
            Transform existing = canvas.transform.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            // Le vieux bouton gris "VOIR LA MENACE", pose a la main sous le canvas
            // avant que le HUD n'ait le sien : il n'a plus de raison d'etre. Tout
            // relais de menace qui n'est pas dans la racine du HUD disparait.
            ThreatPreviewButton[] strays = canvas.GetComponentsInChildren<ThreatPreviewButton>(true);
            for (int i = 0; i < strays.Length; i++)
            {
                if (strays[i] == null) continue;
                Debug.LogFormat("[HudBuilder] Ancien bouton de menace retire : {0}", strays[i].gameObject.name);
                Object.DestroyImmediate(strays[i].gameObject);
            }

            RectTransform root = NewRect(RootName, canvas.transform);
            Stretch(root);

            HudController hud = canvas.gameObject.GetComponent<HudController>();
            if (hud == null) hud = canvas.gameObject.AddComponent<HudController>();

            BuildEnergy(root, hud, labels);
            BuildCenter(root, hud, labels);
            BuildObjectives(root, hud, labels);
            BuildEndTurn(root, hud, labels);
            BuildThreatButton(root, labels);
            BuildHowToPlay(root, canvas);
            BuildPortalPanel(root, canvas, labels);
            BuildTooltip(root, canvas, labels);
            BuildUnitCard(root, canvas, labels);
            BuildTrivia(root, canvas, labels);
            BuildSubjectChoice(root, canvas, labels);
            BuildTankChoice(root, canvas, labels);
            BuildAdvisor(root, canvas, labels);
            BuildHexMenu(root, canvas, labels);
            BuildTeaching(root, canvas);
            BuildTurnAnnounce(root, canvas, labels);
            BuildGameOver(root, canvas, labels);

            ApplyFormats(hud, labels);
            EnsureCameraDirector();
            EnsurePortalTether();
            EnsureElementPhotos();
            BuildImmersive(root, canvas, labels);

            // Le theme electronique en dernier : il habille ce que les Build ont pose.
            ApplyElectronicTheme(root);

            EditorUtility.SetDirty(canvas.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);

            Debug.Log("[HudBuilder] HUD construit et cable. Pense a enregistrer la scene (Ctrl+S).");
            Selection.activeGameObject = root.gameObject;
        }

        // =================================================================
        //  LIBELLES
        // =================================================================
        internal static JObject LoadLabels()
        {
            if (!File.Exists(LabelsPath))
            {
                Debug.LogErrorFormat("[HudBuilder] {0} est introuvable. Cree-le avant de construire le HUD.", LabelsPath);
                return null;
            }

            try
            {
                return JObject.Parse(File.ReadAllText(LabelsPath, Encoding.UTF8));
            }
            catch (System.Exception e)
            {
                Debug.LogErrorFormat("[HudBuilder] {0} est illisible : {1}", LabelsPath, e.Message);
                return null;
            }
        }

        internal static string S(JObject o, string key, string fallback = "")
        {
            JToken t = o != null ? o[key] : null;
            return (t != null) ? t.ToString() : fallback;
        }

        /// <summary>
        /// Cherche une police TMP contenant l'hebreu. Celle des maquettes est Rubik ;
        /// Heebo et Assistant sont des replis acceptables. Sans rien de tout cela, on
        /// prend la police par defaut et on previent : l'hebreu sortira en carrees.
        ///
        /// Le graisse compte : si le projet contient a la fois "Rubik-Regular SDF" et
        /// "Rubik-Bold SDF", il faut imperativement partir du Regular. Retenir le Bold
        /// comme police de base rendrait tout le HUD gras, et FontStyles.Bold n'aurait
        /// plus rien a ajouter - les titres cesseraient de se detacher.
        /// </summary>
        internal static TMP_FontAsset FindHebrewFont()
        {
            string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");

            TMP_FontAsset fallback = null;    // n'importe quoi, en dernier recours
            TMP_FontAsset hebrew = null;      // hebraique, mais graisse non ideale
            TMP_FontAsset regular = null;     // hebraique ET en graisse normale

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                TMP_FontAsset asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (asset == null) continue;

                if (fallback == null) fallback = asset;

                string lower = asset.name.ToLowerInvariant();

                bool isHebrew = lower.Contains("rubik") || lower.Contains("heebo")
                                || lower.Contains("assistant") || lower.Contains("hebrew")
                                || lower.Contains("secular");
                if (!isHebrew) continue;

                if (hebrew == null) hebrew = asset;

                bool heavy = lower.Contains("bold") || lower.Contains("black")
                             || lower.Contains("semibold") || lower.Contains("medium")
                             || lower.Contains("light") || lower.Contains("thin")
                             || lower.Contains("italic");

                if (!heavy && regular == null) regular = asset;
            }

            TMP_FontAsset chosen = (regular != null) ? regular : hebrew;

            if (chosen != null)
            {
                Debug.LogFormat("[HudBuilder] Police retenue : {0}", chosen.name);
                return chosen;
            }

            Debug.LogWarning("[HudBuilder] Aucune police hebraique trouvee (Rubik, Heebo...). "
                           + "Le HUD utilisera la police par defaut et l'hebreu sortira en carrees. "
                           + "Genere un TMP Font Asset Rubik avec la plage "
                           + "0020-007E,00B7,00D7,05B0-05F4,2013-2014,2192 puis relance.");
            return fallback;
        }

        // =================================================================
        //  CANVAS
        // =================================================================
        /// <summary>
        /// Le HUD vit sur SON PROPRE Canvas, jamais sur un Canvas existant.
        ///
        /// C'est delibere : reutiliser le GameCanvas voudrait dire toucher a son
        /// CanvasScaler, et toute l'UI de trivia deja en place se redimensionnerait.
        /// On ne casse rien : on cherche uniquement un Canvas nomme CanvasName, et
        /// s'il n'existe pas on en cree un neuf, a cote.
        /// </summary>
        private static Canvas EnsureCanvas()
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);

            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i].name == CanvasName && canvases[i].transform.parent == null)
                {
                    Configure(canvases[i]);
                    EnsureEventSystem();
                    return canvases[i];
                }
            }

            GameObject go = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Configure(canvas);

            EnsureEventSystem();
            return canvas;
        }

        /// <summary>Sans EventSystem, aucun bouton ne repond. On n'en cree un que s'il manque.</summary>
        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;

            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        private static void Configure(Canvas canvas)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Au-dessus d'un Canvas laisse a 0, pour que le bandeau reste lisible.
            // Si un jour la fenetre de trivia passe dessous, baisse ce chiffre.
            canvas.overrideSorting = false;
            canvas.sortingOrder = SortingOrder;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        // =================================================================
        //  BLOCS
        // =================================================================
        // =================================================================
        //  LES IMAGES DE DAN (Assets/Art/Sprites)
        // =================================================================
        // Le bandeau est fait avec ses cadres, et rien d'autre. Elles sont dessinees
        // au double de la maquette (1280 x 720) ; l'ecran de reference fait 1920 x 1080,
        // donc chaque image s'affiche a 0,75 fois sa taille native, et se place aux
        // cotes de la maquette multipliees par 1,5. Les cadres sont decoupes en neuf
        // (bord 40) pour qu'une plaque puisse etre un peu plus haute ou plus basse
        // sans deformer son bord.
        private const string ArtFolder = "Assets/Art/Sprites/";
        private const float ArtScale = 0.75f;
        private const float ArtBorder = 40f;

        private static Sprite Art(string file, bool sliced)
        {
            string path = ArtFolder + file;
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarningFormat("[HudBuilder] Image de Dan introuvable : {0}", path);
                return null;
            }

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
            if (importer.maxTextureSize < 2048) { importer.maxTextureSize = 2048; dirty = true; }

            Vector4 border = sliced ? new Vector4(ArtBorder, ArtBorder, ArtBorder, ArtBorder) : Vector4.zero;
            if (importer.spriteBorder != border) { importer.spriteBorder = border; dirty = true; }

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteMeshType != SpriteMeshType.FullRect || settings.wrapMode != TextureWrapMode.Clamp)
            {
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.wrapMode = TextureWrapMode.Clamp;
                importer.SetTextureSettings(settings);
                dirty = true;
            }

            if (dirty) importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Un cadre de Dan comme fond d'un bloc : le bloc a la taille du cadre.</summary>
        private static Image ArtPlate(RectTransform block, string file)
        {
            Sprite sprite = Art(file, true);
            RectTransform plate = NewRect("Plaque", block);
            Stretch(plate);
            plate.SetAsFirstSibling();

            Image image = plate.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            if (sprite != null) { image.sprite = sprite; image.type = Image.Type.Sliced; image.color = Color.white; }
            else image.color = ColPanel;
            return image;
        }

        /// <summary>Un bouton de Dan : son image est le targetGraphic, donc l'etat enfonce se voit.</summary>
        private static Button ArtButton(RectTransform block, string file)
        {
            Sprite sprite = Art(file, true);
            Image bg = block.gameObject.AddComponent<Image>();
            if (sprite != null) { bg.sprite = sprite; bg.type = Image.Type.Sliced; bg.color = Color.white; }
            else bg.color = ColPanel;

            Button button = block.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;

            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.78f, 0.86f, 1f, 1f);
            colors.disabledColor = new Color(0.55f, 0.6f, 0.7f, 0.6f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            return button;
        }

        // Les cotes de la maquette, en 1920 x 1080. Les cadres sont poses AVEC leur
        // marge de halo (celle de l'image), d'ou les petits decalages de bord.
        private const float SideW = 408f;          // 544 x 0.75 : les cadres de gauche

        /// <summary>Energie, en haut a DROITE (en hebreu elle ouvre la lecture) : le cadre de Dan, son logo a l'eclair.</summary>
        private static void BuildEnergy(RectTransform root, HudController hud, JObject labels)
        {
            RectTransform block = NewRect("Energie", root);
            AnchorTopRight(block, new Vector2(-11f, -13f), new Vector2(402f, 165f));
            ArtPlate(block, "05_cadre_energie.png");

            Sprite logo = Art("logo_energie.png", false);
            Image badge = NewImage("Logo", block, Color.white);
            badge.preserveAspect = true;
            if (logo != null) badge.sprite = logo;
            AnchorCenterLeft(badge.rectTransform, new Vector2(-201f + 34f, 0f), new Vector2(75f, 87f));

            TextMeshProUGUI label = NewText("Label", block, S(labels, "energy"), 18f, ColText,
                                            TextAlignmentOptions.TopRight, true);
            AnchorTopRight(label.rectTransform, new Vector2(-36f, -26f), new Vector2(240f, 32f));

            TextMeshProUGUI value = NewText("Valeur", block, "0", 42f, ColCyan,
                                            TextAlignmentOptions.BottomRight, false);
            value.fontStyle = FontStyles.Bold;
            AnchorTopRight(value.rectTransform, new Vector2(-36f, -58f), new Vector2(240f, 70f));

            TextMeshProUGUI delta = NewText("Gain", block, "", 20f, new Color32(0x8F, 0xF0, 0xB0, 0xFF),
                                            TextAlignmentOptions.TopRight, true);
            AnchorTopRight(delta.rectTransform, new Vector2(-36f, -168f), new Vector2(240f, 32f));

            hud.energyText = value;
            hud.energyDeltaText = delta;
        }

        /// <summary>Le cadre des phases au centre, le badge du tour a cheval sur son bord haut.</summary>
        private static void BuildCenter(RectTransform root, HudController hud, JObject labels)
        {
            RectTransform block = NewRect("TourEtPhases", root);
            AnchorTopCenter(block, new Vector2(0f, -43f), new Vector2(960f, 138f));
            ArtPlate(block, "03_cadre_phases.png");

            // Le badge du tour : 192 x 132, a 0,75 = 144 x 99, au bord haut de l'ecran.
            Sprite badgeSprite = Art("04_badge_tour.png", false);
            RectTransform hex = NewRect("Badge", block);
            AnchorTopCenter(hex, new Vector2(0f, 43f), new Vector2(144f, 99f));
            Image hexImage = hex.gameObject.AddComponent<Image>();
            hexImage.raycastTarget = false;
            hexImage.preserveAspect = true;
            if (badgeSprite != null) hexImage.sprite = badgeSprite; else hexImage.color = ColPanel;

            TextMeshProUGUI turn = NewText("Tour", hex, "1", 24f, ColText, TextAlignmentOptions.Center, false);
            turn.fontStyle = FontStyles.Bold;
            AnchorCenter(turn.rectTransform, new Vector2(-12f, 0f), new Vector2(60f, 44f));

            TextMeshProUGUI turnWord = NewText("Mot", hex, S(labels, "turn"), 12f, ColDim, TextAlignmentOptions.Center, true);
            AnchorCenter(turnWord.rectTransform, new Vector2(22f, -2f), new Vector2(48f, 30f));

            RectTransform rail = NewRect("Rail", block);
            rail.anchorMin = new Vector2(0f, 0f);
            rail.anchorMax = new Vector2(1f, 1f);
            rail.offsetMin = new Vector2(40f, 18f);
            rail.offsetMax = new Vector2(-40f, -38f);

            HorizontalLayoutGroup layout = rail.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.reverseArrangement = true;   // la premiere phase est a droite

            Sprite barIdle = Art("phase_a_venir.png", false);
            Sprite barDone = Art("phase_passee.png", false);
            Sprite barActive = Art("phase_active.png", false);

            JArray phases = labels["phases"] as JArray;
            HudController.PhaseStep[] steps = new HudController.PhaseStep[5];
            string[] phaseNames = new string[5];

            for (int i = 0; i < 5; i++)
            {
                string name = (phases != null && i < phases.Count) ? phases[i].ToString() : "";
                phaseNames[i] = name;

                RectTransform step = NewRect("Phase" + i, rail);

                VerticalLayoutGroup v = step.gameObject.AddComponent<VerticalLayoutGroup>();
                v.spacing = 6f;
                v.childAlignment = TextAnchor.LowerCenter;
                v.childControlWidth = true;
                v.childControlHeight = true;
                v.childForceExpandWidth = true;
                v.childForceExpandHeight = false;

                TextMeshProUGUI text = NewText("Nom", step, name, 18f, ColIdle,
                                               TextAlignmentOptions.Bottom, true);
                LayoutElement textLayout = text.gameObject.AddComponent<LayoutElement>();
                textLayout.preferredHeight = 34f;

                // Le trait : l'image "a venir" au depart, le controleur change l'image.
                // Hauteur de l'image active (260 x 50 a 0,75 = 38) pour que le halo
                // rouge du Yetzer ait la place ; les traits fins se centrent dedans.
                Image bar = NewImage("Trait", step, Color.white);
                bar.preserveAspect = true;
                if (barIdle != null) bar.sprite = barIdle; else bar.color = ColIdle;
                LayoutElement barLayout = bar.gameObject.AddComponent<LayoutElement>();
                barLayout.preferredHeight = 38f;

                steps[i] = new HudController.PhaseStep();
                steps[i].bar = bar;
                steps[i].label = text;
            }

            hud.turnText = turn;
            hud.phaseSteps = steps;
            hud.phaseLabels = phaseNames;
            hud.phaseIdleSprite = barIdle;
            hud.phaseDoneSprite = barDone;
            hud.phaseActiveSprite = barDone;      // en cours cote joueur : le trait cyan, libelle blanc gras
            hud.phaseEnemySprite = barActive;     // en cours cote Yetzer : le trait rouge lumineux
            hud.phaseIdleColor = ColIdle;
            hud.phaseDoneColor = ColCyan;
            hud.phaseActiveColor = ColCyan;
            hud.phaseEnemyColor = ColRed;
            hud.phaseActiveLabelColor = ColText;
        }

        /// <summary>A GAUCHE : le cadre rouge des Shofars, le cadre des Bases, et un cadre pour les Tanks.</summary>
        private static void BuildObjectives(RectTransform root, HudController hud, JObject labels)
        {
            // --- Shofars : 544 x 226 a 0,75 = 408 x 170 ---
            RectTransform portalRow = NewRect("Shofars", root);
            AnchorTopLeft(portalRow, new Vector2(13f, -10f), new Vector2(SideW, 170f));
            ArtPlate(portalRow, "01_cadre_portes_rouge.png");

            TextMeshProUGUI portalLabel = NewText("Libelle", portalRow, S(labels, "portals"), 18f, ColText,
                                                  TextAlignmentOptions.TopRight, true);
            AnchorTopRight(portalLabel.rectTransform, new Vector2(-34f, -24f), new Vector2(220f, 32f));

            TextMeshProUGUI portalCount = NewText("Compte", portalRow, "6/6", 26f, ColRed,
                                                  TextAlignmentOptions.TopLeft, false);
            portalCount.fontStyle = FontStyles.Bold;
            AnchorTopLeft(portalCount.rectTransform, new Vector2(34f, -18f), new Vector2(120f, 44f));

            RectTransform dots = NewRect("Pastilles", portalRow);
            AnchorBottomCenter(dots, new Vector2(0f, 30f), new Vector2(SideW - 80f, 34f));

            HorizontalLayoutGroup dotLayout = dots.gameObject.AddComponent<HorizontalLayoutGroup>();
            dotLayout.spacing = 10f;
            dotLayout.childAlignment = TextAnchor.MiddleCenter;
            dotLayout.childControlWidth = false;
            dotLayout.childControlHeight = false;

            Sprite dotOn = Art("pastille_rouge.png", false);
            Sprite dotOff = Art("pastille_vide.png", false);
            Image[] portalDots = new Image[6];
            for (int i = 0; i < 6; i++)
            {
                Image dot = NewImage("Pastille" + i, dots, Color.white);
                if (dotOn != null) dot.sprite = dotOn; else dot.color = ColRed;
                dot.preserveAspect = true;
                dot.rectTransform.sizeDelta = new Vector2(27f, 32f);
                portalDots[i] = dot;
            }

            // --- Base : 544 x 208 a 0,75 = 408 x 156 ---
            RectTransform baseRow = NewRect("Base", root);
            AnchorTopLeft(baseRow, new Vector2(13f, -152f), new Vector2(SideW, 156f));
            ArtPlate(baseRow, "02_cadre_bases.png");

            TextMeshProUGUI baseLabel = NewText("Libelle", baseRow, S(labels, "base"), 18f, ColText,
                                                TextAlignmentOptions.TopRight, true);
            AnchorTopRight(baseLabel.rectTransform, new Vector2(-34f, -22f), new Vector2(220f, 32f));

            TextMeshProUGUI baseHp = NewText("PV", baseRow, "200", 26f, ColCyan,
                                             TextAlignmentOptions.TopLeft, false);
            baseHp.fontStyle = FontStyles.Bold;
            AnchorTopLeft(baseHp.rectTransform, new Vector2(34f, -16f), new Vector2(120f, 44f));

            // La jauge : le fond (416 x 20 a 0,75 = 312 x 15) et le remplissage par-dessus
            // (440 x 44 a 0,75 = 330 x 33, halo compris), qui se vide vers la gauche.
            Sprite gaugeBack = Art("jauge_fond.png", false);
            Sprite gaugeFill = Art("jauge_remplissage.png", false);

            Image track = NewImage("Fond", baseRow, Color.white);
            if (gaugeBack != null) track.sprite = gaugeBack; else track.color = new Color32(0x7E, 0xA0, 0xFF, 0x30);
            AnchorBottomCenter(track.rectTransform, new Vector2(0f, 32f), new Vector2(312f, 15f));

            Image fill = NewImage("Remplissage", baseRow, Color.white);
            if (gaugeFill != null) fill.sprite = gaugeFill; else fill.color = ColCyan;
            AnchorBottomCenter(fill.rectTransform, new Vector2(0f, 23f), new Vector2(330f, 33f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Right;   // en RTL, elle se vide vers la gauche
            fill.fillAmount = 1f;

            // --- Tanks : le meme cadre que la Base, plus bas, pour le plafond du commandement ---
            RectTransform tankRow = NewRect("Tanks", root);
            AnchorTopLeft(tankRow, new Vector2(13f, -304f), new Vector2(SideW, 110f));
            ArtPlate(tankRow, "02_cadre_bases.png");

            TextMeshProUGUI tankLabel = NewText("Libelle", tankRow, S(labels, "tanks"), 18f, ColText,
                                                TextAlignmentOptions.Right, true);
            AnchorCenterRight(tankLabel.rectTransform, new Vector2(SideW * 0.5f - 34f, 0f), new Vector2(220f, 34f));

            TextMeshProUGUI tankCount = NewText("Compte", tankRow, "0/10", 26f, ColSteel,
                                                TextAlignmentOptions.Left, false);
            tankCount.fontStyle = FontStyles.Bold;
            AnchorCenterLeft(tankCount.rectTransform, new Vector2(-SideW * 0.5f + 34f, 0f), new Vector2(140f, 44f));

            hud.portalDots = portalDots;
            hud.portalCountText = portalCount;
            hud.portalDotAliveSprite = dotOn;
            hud.portalDotDownSprite = dotOff;
            hud.baseFill = fill;
            hud.baseHpText = baseHp;
            hud.baseHealthyColor = Color.white;    // la jauge est une image : blanc = telle quelle
            hud.baseWarningColor = new Color(1f, 0.85f, 0.55f);
            hud.baseCriticalColor = new Color(1f, 0.45f, 0.5f);
            hud.tankCountText = tankCount;
            hud.tankCapFullColor = ColRed;
        }

        /// <summary>Le bouton de fin de tour de Dan, en bas au centre : 756 x 264 a 0,75 = 567 x 198.</summary>
        private static void BuildEndTurn(RectTransform root, HudController hud, JObject labels)
        {
            RectTransform block = NewRect("FinDeTour", root);
            AnchorBottomCenter(block, new Vector2(0f, 4f), new Vector2(567f, 198f));

            Button button = ArtButton(block, "08_bouton_fin_de_tour.png");

            TextMeshProUGUI text = NewText("Texte", block, S(labels, "endTurn"), 28f, ColText,
                                           TextAlignmentOptions.Center, true);
            text.fontStyle = FontStyles.Bold;
            Stretch(text.rectTransform);

            hud.endTurnButton = button;
            hud.endTurnRoot = block.gameObject;
        }

        /// <summary>La menace : le bouton rouge de Dan, en bas a gauche (544 x 168 a 0,75 = 408 x 126).</summary>
        private static void BuildThreatButton(RectTransform root, JObject labels)
        {
            RectTransform block = NewRect("Menace", root);
            block.anchorMin = new Vector2(0f, 0f);
            block.anchorMax = new Vector2(0f, 0f);
            block.pivot = new Vector2(0f, 0f);
            block.anchoredPosition = new Vector2(13f, 104f);
            block.sizeDelta = new Vector2(SideW, 126f);

            Button button = ArtButton(block, "06_bouton_menace_rouge.png");

            JObject banners = labels["banners"] as JObject;
            TextMeshProUGUI text = NewText("Texte", block, S(banners, "threatShow"), 20f, ColText,
                                           TextAlignmentOptions.Center, true);
            text.fontStyle = FontStyles.Bold;
            Stretch(text.rectTransform);

            ThreatPreviewButton relay = block.gameObject.AddComponent<ThreatPreviewButton>();
            relay.label = text;
            relay.showLabel = S(banners, "threatShow");
            relay.hideLabel = S(banners, "threatHide");
            relay.activeColor = new Color(1f, 0.75f, 0.78f, 1f);
        }

        /// <summary>
        /// COMMENT ON JOUE : un bouton du bandeau, et la page qu'il ouvre.
        ///
        /// TROISIEME MISE EN PAGE, ET LA BONNE RAISON
        ///
        /// Les consignes ont d'abord defile a chaque ouverture de tour - trop lourd
        /// pour un moment qui doit rester court. Puis la legende a ete posee en
        /// permanence sur le bandeau - trop petite pour etre lue, assez grande pour
        /// encombrer.
        ///
        /// Derriere un bouton, la contrainte disparait : l'aide ne vole de place a
        /// personne, donc elle peut enfin etre GRANDE et COMPLETE. C'est la seule forme
        /// ou les trois exigences tiennent ensemble.
        /// </summary>
        private static void BuildHowToPlay(RectTransform root, Canvas canvas)
        {
            HowToPlayPanel ctrl = canvas.gameObject.GetComponent<HowToPlayPanel>();
            if (ctrl == null) ctrl = canvas.gameObject.AddComponent<HowToPlayPanel>();

            BuildHelpButton(root, ctrl);
            BuildHelpPage(root, canvas, ctrl);
        }

        /// <summary>
        /// Le bouton, en bas a gauche. A l'oppose du bouton de fin de tour : on ne doit
        /// jamais ouvrir l'aide en croyant finir son tour.
        /// </summary>
        private static void BuildHelpButton(RectTransform root, HowToPlayPanel ctrl)
        {
            RectTransform block = NewRect("BoutonAide", root);
            block.anchorMin = new Vector2(0f, 0f);
            block.anchorMax = new Vector2(0f, 0f);
            block.pivot = new Vector2(0f, 0f);
            block.anchoredPosition = new Vector2(37f, 36f);
            block.sizeDelta = new Vector2(360f, 78f);

            Button button = ArtButton(block, "07_bouton_comment_jouer.png");

            // PAS de onClick.AddListener ici : un abonnement pose depuis l'editeur n'est
            // pas enregistre dans la scene. Le panneau s'abonne dans son Awake.
            ctrl.openButton = button;

            TextMeshProUGUI label = NewText("Libelle", block, "", 20f, ColText,
                                            TextAlignmentOptions.Center, true);
            label.fontStyle = FontStyles.Bold;
            Stretch(label.rectTransform);
            label.text = ReadTeachingLabel("helpButton");
        }

        /// <summary>La meme page d'aide, pour le menu principal.</summary>
        internal static void BuildHelpPageInto(RectTransform root, Canvas canvas, HowToPlayPanel ctrl)
        {
            _font = FindHebrewFont();
            BuildHelpPage(root, canvas, ctrl);
        }

        internal static string ReadTeachingLabelPublic(string key)
        {
            return ReadTeachingLabel(key);
        }

        /// <summary>Un lecteur d'enseignements sur l'objet donne, s'il n'y en a aucun dans la scene.</summary>
        internal static void EnsureTeachingManagerIn(GameObject host)
        {
            TeachingManager existing = Object.FindFirstObjectByType<TeachingManager>(FindObjectsInactive.Include);
            if (existing != null) return;
            host.AddComponent<TeachingManager>();
        }

        /// <summary>La page : les consignes en haut, la legende en bas.</summary>
        private static void BuildHelpPage(RectTransform root, Canvas canvas, HowToPlayPanel ctrl)
        {
            const float PageW = 1120f;
            const float PageH = 880f;
            const float Pad = 44f;

            RectTransform panel = NewRect("CommentJouer", root);
            Stretch(panel);

            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = true;

            Image dim = panel.gameObject.AddComponent<Image>();
            dim.color = new Color32(0x03, 0x05, 0x0B, 0xE0);
            dim.raycastTarget = true;

            RectTransform page = NewRect("Page", panel);
            AnchorCenter(page, Vector2.zero, new Vector2(PageW, PageH));

            Image pageBg = page.gameObject.AddComponent<Image>();
            pageBg.color = new Color32(0x0B, 0x11, 0x20, 0xF2);
            pageBg.raycastTarget = false;

            UnityEngine.UI.Outline frame = page.gameObject.AddComponent<UnityEngine.UI.Outline>();
            frame.effectColor = new Color(ColGold.r, ColGold.g, ColGold.b, 0.34f);
            frame.effectDistance = new Vector2(1f, 1f);

            Image accent = NewImage("Liseret", page, ColGold);
            AnchorTopRight(accent.rectTransform, Vector2.zero, new Vector2(PageW, 3f));

            TextMeshProUGUI header = NewText("Titre", page, "", 34f, ColGold,
                                             TextAlignmentOptions.TopRight, true);
            header.fontStyle = FontStyles.Bold;
            AnchorTopRight(header.rectTransform, new Vector2(-Pad, -28f), new Vector2(760f, 50f));

            TextMeshProUGUI tagline = NewText("SousTitre", page, "", 18f, ColDim,
                                              TextAlignmentOptions.TopRight, true);
            AnchorTopRight(tagline.rectTransform, new Vector2(-Pad, -78f), new Vector2(760f, 30f));

            Image rule = NewImage("Filet", page, ColLine);
            AnchorTopCenter(rule.rectTransform, new Vector2(0f, -116f), new Vector2(PageW - Pad * 2f, 1f));

            // --- les cinq consignes ---
            GameObject[] stepRoots = new GameObject[HowToPlayPanel.MaxSteps];
            Image[] stepIcons = new Image[HowToPlayPanel.MaxSteps];
            TextMeshProUGUI[] stepTitles = new TextMeshProUGUI[HowToPlayPanel.MaxSteps];
            TextMeshProUGUI[] stepBodies = new TextMeshProUGUI[HowToPlayPanel.MaxSteps];

            const float StepH = 98f;
            const float IconW = 58f;
            float textW = PageW - Pad * 2f - IconW - 22f;

            for (int i = 0; i < HowToPlayPanel.MaxSteps; i++)
            {
                RectTransform row = NewRect("Etape" + i, page);
                AnchorTopCenter(row, new Vector2(0f, -136f - i * StepH),
                                new Vector2(PageW - Pad * 2f, StepH - 10f));

                // Une seule colonne de pictogrammes, a droite, toujours au meme x :
                // l'oeil descend en ligne droite au lieu de chercher chaque ligne.
                Image icon = NewImage("Icone", row, ColGold);
                AnchorCenterRight(icon.rectTransform, Vector2.zero, new Vector2(IconW, IconW));
                icon.preserveAspect = true;
                Bind(icon, StepIcon(i));

                TextMeshProUGUI title = NewText("Titre", row, "", 25f, ColText,
                                                TextAlignmentOptions.TopRight, true);
                title.fontStyle = FontStyles.Bold;
                AnchorTopRight(title.rectTransform, new Vector2(-(IconW + 22f), -2f), new Vector2(textW, 36f));

                TextMeshProUGUI body = NewText("Texte", row, "", 19f, ColDim,
                                               TextAlignmentOptions.TopRight, true);
                body.enableWordWrapping = true;
                body.lineSpacing = 6f;
                AnchorTopRight(body.rectTransform, new Vector2(-(IconW + 22f), -40f), new Vector2(textW, 48f));

                stepRoots[i] = row.gameObject;
                stepIcons[i] = icon;
                stepTitles[i] = title;
                stepBodies[i] = body;
            }

            // --- la legende, sous les consignes ---
            HudLegend legend = BuildLegendBlock(page, canvas, PageW, Pad);

            // --- fermer ---
            RectTransform close = NewRect("Fermer", page);
            AnchorBottomCenter(close, new Vector2(0f, 26f), new Vector2(300f, 62f));

            Image closeBg = close.gameObject.AddComponent<Image>();
            closeBg.color = new Color(ColGold.r, ColGold.g, ColGold.b, 0.16f);

            UnityEngine.UI.Outline closeEdge = close.gameObject.AddComponent<UnityEngine.UI.Outline>();
            closeEdge.effectColor = new Color(ColGold.r, ColGold.g, ColGold.b, 0.55f);
            closeEdge.effectDistance = new Vector2(1.5f, 1.5f);

            Button closeButton = close.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeBg;

            TextMeshProUGUI closeLabel = NewText("Libelle", close, "", 23f, ColGold,
                                                 TextAlignmentOptions.Center, true);
            Stretch(closeLabel.rectTransform);

            // --- cablage ---
            ctrl.panelRoot = panel.gameObject;
            ctrl.group = group;
            ctrl.headerText = header;
            ctrl.taglineText = tagline;
            ctrl.stepRoots = stepRoots;
            ctrl.stepIcons = stepIcons;
            ctrl.stepTitles = stepTitles;
            ctrl.stepBodies = stepBodies;
            ctrl.legend = legend;
            ctrl.closeButton = closeButton;
            ctrl.closeText = closeLabel;

            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Le bloc de legende : deux colonnes de cinq, sous les consignes.
        ///
        /// Deux colonnes et non dix lignes : un bloc compact se balaie d'un regard,
        /// une colonne de dix se lit une entree a la fois.
        /// </summary>
        private static HudLegend BuildLegendBlock(RectTransform page, Canvas canvas, float pageW, float pad)
        {
            const int Cols = 2;
            const float CellH = 52f;

            int count = HudLegend.MaxEntries;
            int rows = (count + Cols - 1) / Cols;
            float cellW = (pageW - pad * 2f) / Cols;

            Image rule = NewImage("Filet_Legende", page, ColLine);
            AnchorTopCenter(rule.rectTransform, new Vector2(0f, -640f), new Vector2(pageW - pad * 2f, 1f));

            TextMeshProUGUI header = NewText("Legende_Titre", page, "", 18f, ColDim,
                                             TextAlignmentOptions.TopRight, true);
            AnchorTopRight(header.rectTransform, new Vector2(-pad, -664f), new Vector2(600f, 28f));

            HudLegend legend = canvas.gameObject.GetComponent<HudLegend>();
            if (legend == null) legend = canvas.gameObject.AddComponent<HudLegend>();

            GameObject[] roots = new GameObject[count];
            Image[] icons = new Image[count];
            TextMeshProUGUI[] names = new TextMeshProUGUI[count];

            // Meme ordre que la section "legend" de teachings.json.
            MNLTHII.UI.IconKind[] order =
            {
                MNLTHII.UI.IconKind.Factory, MNLTHII.UI.IconKind.Crystal,
                MNLTHII.UI.IconKind.Bunker,  MNLTHII.UI.IconKind.Command,
                MNLTHII.UI.IconKind.Tank,    MNLTHII.UI.IconKind.Portal,
                MNLTHII.UI.IconKind.Crack,   MNLTHII.UI.IconKind.Energy,
                MNLTHII.UI.IconKind.Enemy,   MNLTHII.UI.IconKind.Base
            };

            Color[] tints =
            {
                new Color(0.42f, 0.92f, 0.62f), new Color(0.30f, 0.94f, 0.86f),
                ColGold,                        new Color(1f, 0.54f, 0.24f),
                new Color(0.37f, 0.66f, 1f),    ColOrange,
                new Color(1f, 0.66f, 0.30f),    ColCyan,
                ColRed,                         ColText
            };

            for (int i = 0; i < count; i++)
            {
                int row = i / Cols;
                int col = i % Cols;

                // La premiere colonne est la plus a DROITE : la lecture hebraique
                // commence de ce cote, et la legende doit suivre la meme regle que le
                // reste de l'interface.
                float x = (Cols - 1 - col) * cellW - (pageW - pad * 2f) * 0.5f + cellW * 0.5f;
                float y = -694f - row * CellH;

                RectTransform cell = NewRect("Entree" + i, page);
                AnchorTopCenter(cell, new Vector2(x, y), new Vector2(cellW - 12f, CellH - 6f));

                Image icon = NewImage("Icone", cell, tints[i]);
                AnchorCenterRight(icon.rectTransform, new Vector2(-8f, 0f), new Vector2(36f, 36f));
                icon.preserveAspect = true;
                Bind(icon, order[i]);

                TextMeshProUGUI name = NewText("Nom", cell, "", 20f, ColDim,
                                               TextAlignmentOptions.Right, true);
                AnchorCenterRight(name.rectTransform, new Vector2(-54f, 0f),
                                  new Vector2(cellW - 76f, CellH - 12f));

                roots[i] = cell.gameObject;
                icons[i] = icon;
                names[i] = name;
            }

            legend.headerText = header;
            legend.entryRoots = roots;
            legend.entryIcons = icons;
            legend.entryNames = names;
            legend.iconColors = tints;

            return legend;
        }

        /// <summary>
        /// Le pictogramme de chaque consigne, dans l'ordre du fichier :
        /// usine, Tank, danger, fissure, attaque.
        ///
        /// Cette table vit ici et pas dans le JSON : l'ordre des cinq consignes est la
        /// boucle du jeu elle-meme, il ne se reordonne pas au gre d'une traduction.
        /// </summary>
        private static MNLTHII.UI.IconKind StepIcon(int index)
        {
            switch (index)
            {
                case 0: return MNLTHII.UI.IconKind.Factory;
                case 1: return MNLTHII.UI.IconKind.Tank;
                case 2: return MNLTHII.UI.IconKind.Enemy;
                case 3: return MNLTHII.UI.IconKind.Crack;
                case 4: return MNLTHII.UI.IconKind.Portal;
            }
            return MNLTHII.UI.IconKind.None;
        }

        /// <summary>
        /// Lit un libelle de teachings.json au moment de la construction.
        ///
        /// Le bouton d'aide est le seul element dont le texte doit exister AVANT que le
        /// jeu tourne : il est visible des la premiere image, et un bouton muet dans un
        /// coin n'invite personne a cliquer.
        /// </summary>
        private static string ReadTeachingLabel(string key)
        {
            const string path = "Assets/Resources/teachings.json";

            if (!File.Exists(path)) return "";

            try
            {
                JObject root = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
                JObject ui = (root != null) ? root["ui"] as JObject : null;
                return S(ui, key);
            }
            catch (System.Exception e)
            {
                Debug.LogWarningFormat("[HudBuilder] Lecture de {0} impossible : {1}", path, e.Message);
                return "";
            }
        }

        /// <summary>L'info-bulle de cout. Masquee au depart : elle ne parait qu'au survol.</summary>
        private static void BuildTooltip(RectTransform root, Canvas canvas, JObject labels)
        {
            JObject t = labels["tooltip"] as JObject;

            // Voile des details (mode clic) : meme teinte que l'ecran de choix du Tank.
            Image detailsDim = NewImage("InfoBulle_Voile", root, new Color32(0x03, 0x05, 0x0B, 0xDB));
            Stretch(detailsDim.rectTransform);
            detailsDim.raycastTarget = true;
            detailsDim.gameObject.SetActive(false);

            RectTransform panel = NewRect("InfoBulle", root);
            // Plus haut qu'avant : la phrase qui dit ce que le batiment APPORTE occupe
            // deux a trois lignes sous les statistiques.
            AnchorTopLeft(panel, new Vector2(400f, -400f), new Vector2(470f, 440f));
            panel.pivot = new Vector2(0f, 1f);

            Image bg = panel.gameObject.AddComponent<Image>();
            bg.color = ColPanel;
            bg.raycastTarget = false;

            UnityEngine.UI.Outline outline = panel.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = ColLine;
            outline.effectDistance = new Vector2(1f, 1f);

            // --- la photo de l'element (bandeau du haut, masque s'il n'y a pas de photo) ---
            // Le cadre coupe ce qui depasse ; l'image dedans remplit le cadre sans se
            // deformer (EnvelopeParent) : une photo carree est recadree, pas ecrasee.
            RectTransform photoFrame = NewRect("Photo", panel);
            photoFrame.anchorMin = new Vector2(0f, 1f);
            photoFrame.anchorMax = new Vector2(1f, 1f);
            photoFrame.pivot = new Vector2(0.5f, 1f);
            photoFrame.anchoredPosition = Vector2.zero;
            photoFrame.sizeDelta = new Vector2(-2f, TooltipPhotoHeight);
            photoFrame.gameObject.AddComponent<RectMask2D>();

            Image photoBack = photoFrame.gameObject.AddComponent<Image>();
            photoBack.color = new Color32(0x05, 0x08, 0x12, 0xFF);
            photoBack.raycastTarget = false;

            Image photo = NewImage("Image", photoFrame, Color.white);
            photo.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            photo.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            photo.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            photo.rectTransform.anchoredPosition = Vector2.zero;
            AspectRatioFitter photoFit = photo.gameObject.AddComponent<AspectRatioFitter>();
            photoFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            photoFit.aspectRatio = 1f;

            Image photoRule = NewImage("Filet", photoFrame, ColLine);
            photoRule.rectTransform.anchorMin = new Vector2(0f, 0f);
            photoRule.rectTransform.anchorMax = new Vector2(1f, 0f);
            photoRule.rectTransform.pivot = new Vector2(0.5f, 0f);
            photoRule.rectTransform.anchoredPosition = Vector2.zero;
            photoRule.rectTransform.sizeDelta = new Vector2(0f, 1f);

            photoFrame.gameObject.SetActive(false);

            // --- la rangee des evolutions : un niveau par vignette, de droite a gauche
            //     (sens de lecture hebreu), le niveau actuel en cyan, le prochain en or ---
            RectTransform evo = NewRect("Evolutions", panel);
            evo.anchorMin = new Vector2(0f, 1f);
            evo.anchorMax = new Vector2(1f, 1f);
            evo.pivot = new Vector2(0.5f, 1f);
            evo.anchoredPosition = Vector2.zero;
            evo.sizeDelta = new Vector2(0f, TooltipEvolutionHeight);

            const float Thumb = 104f;
            const float SlotGap = 140f;

            GameObject[] evoSlots = new GameObject[3];
            Image[] evoFrames = new Image[3];
            UnityEngine.UI.Outline[] evoOutlines = new UnityEngine.UI.Outline[3];
            Image[] evoThumbs = new Image[3];
            TextMeshProUGUI[] evoLabels = new TextMeshProUGUI[3];

            for (int i = 0; i < 3; i++)
            {
                RectTransform slot = NewRect("Niveau" + i, evo);
                AnchorTopCenter(slot, new Vector2(SlotGap - i * SlotGap, -14f), new Vector2(Thumb, Thumb + 32f));

                Image frameImg = NewImage("Cadre", slot, new Color(1f, 1f, 1f, 0.05f));
                AnchorTopCenter(frameImg.rectTransform, Vector2.zero, new Vector2(Thumb, Thumb));

                UnityEngine.UI.Outline edge = frameImg.gameObject.AddComponent<UnityEngine.UI.Outline>();
                edge.effectColor = ColLine;
                edge.effectDistance = new Vector2(2f, 2f);

                Image thumb = NewImage("Vignette", frameImg.rectTransform, Color.white);
                thumb.rectTransform.anchorMin = Vector2.zero;
                thumb.rectTransform.anchorMax = Vector2.one;
                thumb.rectTransform.offsetMin = new Vector2(5f, 5f);
                thumb.rectTransform.offsetMax = new Vector2(-5f, -5f);
                thumb.preserveAspect = true;

                TextMeshProUGUI label = NewText("Libelle", slot, "", 15f, ColDim,
                                                TextAlignmentOptions.Center, true);
                AnchorBottomCenter(label.rectTransform, Vector2.zero, new Vector2(Thumb + 20f, 26f));

                evoSlots[i] = slot.gameObject;
                evoFrames[i] = frameImg;
                evoOutlines[i] = edge;
                evoThumbs[i] = thumb;
                evoLabels[i] = label;

                // Une fleche entre deux vignettes, pointee dans le sens de la progression.
                if (i < 2)
                {
                    TextMeshProUGUI arrow = NewText("Fleche" + i, evo, "<", 22f, ColDim,
                                                    TextAlignmentOptions.Center, false);
                    AnchorTopCenter(arrow.rectTransform,
                                    new Vector2(SlotGap - i * SlotGap - SlotGap * 0.5f, -14f - Thumb * 0.5f + 14f),
                                    new Vector2(30f, 28f));
                }
            }

            evo.gameObject.SetActive(false);

            // --- le texte, dans un bloc qu'on pousse vers le bas quand la photo est la ---
            RectTransform content = NewRect("Contenu", panel);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 440f);

            Image kindIcon = NewImage("Icone", content, ColText);
            AnchorTopRight(kindIcon.rectTransform, new Vector2(-26f, -24f), new Vector2(34f, 34f));
            kindIcon.preserveAspect = true;
            Bind(kindIcon, MNLTHII.UI.IconKind.Tank);   // remplace a chaque survol

            TextMeshProUGUI title = NewText("Titre", content, "", 26f, ColText, TextAlignmentOptions.TopRight, true);
            AnchorTopRight(title.rectTransform, new Vector2(-70f, -22f), new Vector2(280f, 46f));
            title.fontStyle = FontStyles.Bold;

            TextMeshProUGUI cost = NewText("Cout", content, "0", 30f, ColGold, TextAlignmentOptions.TopLeft, false);
            AnchorTopLeft(cost.rectTransform, new Vector2(26f, -22f), new Vector2(120f, 50f));
            cost.fontStyle = FontStyles.Bold;

            TextMeshProUGUI subtitle = NewText("SousTitre", content, "", 19f, ColDim, TextAlignmentOptions.TopRight, true);
            AnchorTopRight(subtitle.rectTransform, new Vector2(-26f, -72f), new Vector2(360f, 36f));

            // Trois lignes de detail : libelle a droite, valeur a gauche.
            TextMeshProUGUI[] detailLabels = new TextMeshProUGUI[3];
            TextMeshProUGUI[] detailValues = new TextMeshProUGUI[3];

            for (int i = 0; i < 3; i++)
            {
                float y = -122f - i * 40f;

                TextMeshProUGUI label = NewText("Detail" + i + "_Libelle", content, "", 19f, ColDim,
                                                TextAlignmentOptions.TopRight, true);
                AnchorTopRight(label.rectTransform, new Vector2(-26f, y), new Vector2(270f, 36f));

                TextMeshProUGUI value = NewText("Detail" + i + "_Valeur", content, "", 19f, ColText,
                                                TextAlignmentOptions.TopLeft, false);
                AnchorTopLeft(value.rectTransform, new Vector2(26f, y), new Vector2(150f, 36f));

                detailLabels[i] = label;
                detailValues[i] = value;
            }

            TextMeshProUGUI after = NewText("SoldeApres", content, "", 19f, new Color32(0x7F, 0xE8, 0xA0, 0xFF),
                                            TextAlignmentOptions.TopLeft, false);
            AnchorTopLeft(after.rectTransform, new Vector2(26f, -256f), new Vector2(180f, 36f));

            TextMeshProUGUI afterLabel = NewText("SoldeApres_Libelle", content, S(t, "afterBalance"), 19f, ColDim,
                                                 TextAlignmentOptions.TopRight, true);
            AnchorTopRight(afterLabel.rectTransform, new Vector2(-26f, -256f), new Vector2(270f, 36f));

            Image rule = NewImage("Filet", content, ColLine);
            AnchorTopRight(rule.rectTransform, new Vector2(-26f, -294f), new Vector2(418f, 1f));

            // LA PHRASE. Les chiffres au-dessus disent COMBIEN ; celle-ci dit a quoi le
            // batiment sert. Sans elle, un joueur peut finir une partie entiere sans
            // avoir compris la difference entre le Gaz et le Cristal.
            TextMeshProUGUI effect = NewText("Effet", content, "", 17f, ColDim,
                                             TextAlignmentOptions.TopRight, true);
            effect.enableWordWrapping = true;
            effect.lineSpacing = 8f;
            AnchorTopRight(effect.rectTransform, new Vector2(-26f, -308f), new Vector2(418f, 112f));

            HexTooltipController tooltip = canvas.gameObject.GetComponent<HexTooltipController>();
            if (tooltip == null) tooltip = canvas.gameObject.AddComponent<HexTooltipController>();

            tooltip.panelRoot = panel.gameObject;
            tooltip.titleText = title;
            tooltip.subtitleText = subtitle;
            tooltip.costText = cost;
            tooltip.afterBalanceText = after;
            tooltip.detailLabels = detailLabels;
            tooltip.detailValues = detailValues;
            tooltip.effectText = effect;
            tooltip.kindIcon = kindIcon;
            tooltip.photoRoot = photoFrame.gameObject;
            tooltip.photoImage = photo;
            tooltip.photoFitter = photoFit;
            tooltip.contentRoot = content;
            tooltip.photoHeight = TooltipPhotoHeight;
            tooltip.evolutionRoot = evo;
            tooltip.evolutionSlots = evoSlots;
            tooltip.evolutionFrames = evoFrames;
            tooltip.evolutionOutlines = evoOutlines;
            tooltip.evolutionThumbs = evoThumbs;
            tooltip.evolutionLabels = evoLabels;
            tooltip.evolutionHeight = TooltipEvolutionHeight;
            tooltip.evolutionLevelFormat = S(t, "evolutionLevel", "{0}");
            tooltip.evolutionNaturalLabel = S(t, "evolutionNatural");
            tooltip.followCursor = false;
            tooltip.detailsDim = detailsDim.gameObject;

            tooltip.labelTankCreate = S(t, "tankCreate");
            tooltip.labelTankEvolve = S(t, "tankEvolve");
            tooltip.labelStanceChange = S(t, "stanceChange");
            tooltip.labelBunker = S(t, "bunker");
            tooltip.labelGas = S(t, "gas");
            tooltip.labelCrystal = S(t, "crystal");
            tooltip.labelMountain = S(t, "mountain");
            tooltip.labelMaxLevel = S(t, "maxLevel");
            tooltip.labelOccupied = S(t, "occupied");
            tooltip.labelFree = S(t, "free");

            tooltip.labelShotsPerTurn = S(t, "shotsPerTurn");
            tooltip.labelRange = S(t, "range");
            tooltip.labelShotCost = S(t, "shotCost");
            tooltip.labelHeal = S(t, "heal");
            tooltip.labelIncome = S(t, "income");
            tooltip.labelMaxHpBonus = S(t, "maxHpBonus");
            tooltip.labelDamage = S(t, "damage");
            tooltip.labelHitPoints = S(t, "hitPoints");
            tooltip.labelCommandRadius = S(t, "commandRadius");
            tooltip.labelMoveBonus = S(t, "moveBonus");
            tooltip.labelDecay = S(t, "decay");

            // Textes d'effet : cles uniques, relues a chaud par HexTooltipController.
            // Celles-ci ne sont que le secours grave dans la scene.
            tooltip.effectBunker = S(t, "effectBunker");
            tooltip.effectGas = S(t, "effectGas");
            tooltip.effectCrystal = S(t, "effectCrystal");
            tooltip.effectMountain = S(t, "effectMountain");
            tooltip.effectTankCreate = S(t, "effectTankCreate");
            tooltip.effectTankEvolve = S(t, "effectTankEvolve");

            tooltip.subtitleLevelFormat = S(labels, "subtitleLevelFormat", "{0}");

            // Memes noms de posture que la carte d'unite : une seule traduction, au
            // meme endroit. Deux listes finiraient par diverger.
            tooltip.stanceNames = ReadArray(labels["unit"] as JObject, "stances", 3);

            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Le panneau d'etat d'un Shofar. Sous le bloc d'energie, a droite : en
        /// hebreu la droite ouvre la lecture, et c'est l'information la plus
        /// importante de la phase de depense.
        /// </summary>
        private static void BuildPortalPanel(RectTransform root, Canvas canvas, JObject labels)
        {
            JObject p = labels["portal"] as JObject;

            const float W = 420f;
            const float Pad = 26f;
            const float Inner = W - Pad * 2f;

            RectTransform panel = NewRect("PanneauShofar", root);
            AnchorTopRight(panel, new Vector2(-56f, -236f), new Vector2(W, 392f));

            Image bg = panel.gameObject.AddComponent<Image>();
            bg.color = ColPanel;
            bg.raycastTarget = false;

            UnityEngine.UI.Outline frame = panel.gameObject.AddComponent<UnityEngine.UI.Outline>();
            frame.effectColor = new Color(ColOrange.r, ColOrange.g, ColOrange.b, 0.24f);
            frame.effectDistance = new Vector2(1f, 1f);

            // Lisere du haut : sa couleur dit l'etat du bouclier d'un seul coup d'oeil.
            Image accent = NewImage("Liseret", panel, ColOrange);
            AnchorTopRight(accent.rectTransform, Vector2.zero, new Vector2(W, 2f));

            // --- entete ---
            TextMeshProUGUI title = NewText("Nom", panel, "", 20f, ColText,
                                            TextAlignmentOptions.TopRight, true);
            title.fontStyle = FontStyles.Bold;
            AnchorTopRight(title.rectTransform, new Vector2(-Pad, -22f), new Vector2(250f, 38f));

            TextMeshProUGUI level = NewText("Niveau", panel, "", 15f, ColDim,
                                            TextAlignmentOptions.TopLeft, true);
            AnchorTopLeft(level.rectTransform, new Vector2(Pad, -26f), new Vector2(120f, 32f));

            // --- points de vie ---
            TextMeshProUGUI hp = NewText("PV", panel, "", 15f, ColOrange,
                                          TextAlignmentOptions.TopRight, false);
            hp.fontStyle = FontStyles.Bold;
            AnchorTopRight(hp.rectTransform, new Vector2(-Pad, -74f), new Vector2(100f, 30f));

            Image track = NewImage("Jauge_Fond", panel, new Color32(0x7E, 0xA0, 0xFF, 0x21));
            AnchorTopRight(track.rectTransform, new Vector2(-(Pad + 110f), -80f),
                           new Vector2(Inner - 110f, 7f));

            Image hpFill = NewImage("Jauge", track.rectTransform, ColOrange);
            Stretch(hpFill.rectTransform);
            hpFill.type = Image.Type.Filled;
            hpFill.fillMethod = Image.FillMethod.Horizontal;
            // En RTL la jauge se vide vers la gauche : elle se remplit depuis la droite.
            hpFill.fillOrigin = (int)Image.OriginHorizontal.Right;
            hpFill.fillAmount = 1f;

            // Le plancher du contrecoup : sous ce trait, tuer des ennemis ne fait
            // plus rien. C'est ce qui interdit la partie 100 % defensive, donc c'est
            // exactement le genre de regle qui doit etre VISIBLE.
            float floorRatio = InteractionRules.PORTAL_BACKLASH_FLOOR_PERCENT / 100f;
            Image floorMark = NewImage("Plancher", track.rectTransform, new Color32(0xFF, 0xFF, 0xFF, 0x73));
            AnchorTopRight(floorMark.rectTransform,
                           new Vector2(-(Inner - 110f) * floorRatio, 5f),
                           new Vector2(2f, 17f));

            TextMeshProUGUI note = NewText("Note", panel, S(p, "floorNote"), 12f,
                                            new Color32(0x6E, 0x7C, 0x9E, 0xFF),
                                            TextAlignmentOptions.TopRight, true);
            AnchorTopRight(note.rectTransform, new Vector2(-Pad, -100f), new Vector2(Inner, 56f));
            note.enableWordWrapping = true;

            // --- separateur ---
            Image rule = NewImage("Filet", panel, ColLine);
            AnchorTopRight(rule.rectTransform, new Vector2(-Pad, -166f), new Vector2(Inner, 1f));

            // --- instabilite ---
            TextMeshProUGUI instLabel = NewText("Instabilite_Libelle", panel, S(p, "instability"), 14f, ColDim,
                                                 TextAlignmentOptions.TopRight, true);
            AnchorTopRight(instLabel.rectTransform, new Vector2(-Pad, -184f), new Vector2(230f, 32f));

            TextMeshProUGUI instValue = NewText("Instabilite_Valeur", panel, "", 15f, ColOrange,
                                                 TextAlignmentOptions.TopLeft, false);
            instValue.fontStyle = FontStyles.Bold;
            AnchorTopLeft(instValue.rectTransform, new Vector2(Pad, -184f), new Vector2(110f, 32f));

            RectTransform segRow = NewRect("Instabilite_Segments", panel);
            AnchorTopRight(segRow, new Vector2(-Pad, -222f), new Vector2(Inner, 7f));

            HorizontalLayoutGroup segLayout = segRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            segLayout.spacing = 4f;
            segLayout.childControlWidth = true;
            segLayout.childControlHeight = true;
            segLayout.childForceExpandWidth = true;
            segLayout.childForceExpandHeight = true;
            // Les morts se comptent de droite a gauche, comme tout le reste.
            segLayout.reverseArrangement = true;

            int needed = InteractionRules.PORTAL_KILLS_TO_BREAK_SHIELD;
            Image[] segments = new Image[needed];
            for (int i = 0; i < needed; i++)
                segments[i] = NewImage("Segment" + i, segRow, ColLine);

            // --- encadre du bouclier ---
            RectTransform box = NewRect("Bouclier", panel);
            AnchorTopRight(box, new Vector2(-Pad, -248f), new Vector2(Inner, 92f));

            Image boxBg = box.gameObject.AddComponent<Image>();
            boxBg.color = new Color32(0x7E, 0xA0, 0xFF, 0x0D);
            boxBg.raycastTarget = false;

            UnityEngine.UI.Outline boxLine = box.gameObject.AddComponent<UnityEngine.UI.Outline>();
            boxLine.effectColor = ColLine;
            boxLine.effectDistance = new Vector2(1f, 1f);

            TextMeshProUGUI shieldTitle = NewText("Titre", box, "", 16f, ColDim,
                                                   TextAlignmentOptions.TopRight, true);
            shieldTitle.fontStyle = FontStyles.Bold;
            AnchorTopRight(shieldTitle.rectTransform, new Vector2(-18f, -14f), new Vector2(300f, 32f));

            TextMeshProUGUI shieldBody = NewText("Corps", box, "", 13f,
                                                  new Color32(0x8A, 0x97, 0xB8, 0xFF),
                                                  TextAlignmentOptions.TopRight, true);
            AnchorTopRight(shieldBody.rectTransform, new Vector2(-18f, -50f), new Vector2(330f, 32f));

            // --- evolution ---
            RectTransform evolveRow = NewRect("Evolution", panel);
            AnchorTopRight(evolveRow, new Vector2(-Pad, -352f), new Vector2(Inner, 32f));

            TextMeshProUGUI evolveLabel = NewText("Libelle", evolveRow, S(p, "evolveLabel"), 14f, ColDim,
                                                   TextAlignmentOptions.TopRight, true);
            AnchorTopRight(evolveLabel.rectTransform, Vector2.zero, new Vector2(250f, 32f));

            TextMeshProUGUI evolveValue = NewText("Valeur", evolveRow, "", 14f, ColGold,
                                                   TextAlignmentOptions.TopLeft, true);
            evolveValue.fontStyle = FontStyles.Bold;
            AnchorTopLeft(evolveValue.rectTransform, Vector2.zero, new Vector2(140f, 32f));

            // --- cablage ---
            PortalPanelController ctrl = canvas.gameObject.GetComponent<PortalPanelController>();
            if (ctrl == null) ctrl = canvas.gameObject.AddComponent<PortalPanelController>();

            ctrl.panelRoot = panel.gameObject;
            ctrl.accentBar = accent;
            ctrl.titleText = title;
            ctrl.levelText = level;
            ctrl.hpText = hp;
            ctrl.hpFill = hpFill;
            ctrl.instabilityText = instValue;
            ctrl.instabilitySegments = segments;
            ctrl.shieldBox = boxBg;
            ctrl.shieldTitleText = shieldTitle;
            ctrl.shieldBodyText = shieldBody;
            ctrl.evolveRow = evolveRow.gameObject;
            ctrl.evolveText = evolveValue;

            JArray dirs = (p != null) ? p["directions"] as JArray : null;
            string[] names = new string[6];
            for (int i = 0; i < 6; i++)
                names[i] = (dirs != null && i < dirs.Count) ? dirs[i].ToString() : "";
            ctrl.directionNames = names;

            ctrl.levelFormat = S(p, "levelFormat", "{0}");
            ctrl.hpFormat = S(p, "hpFormat", "{0}/{1}");
            ctrl.instabilityFormat = S(p, "instabilityFormat", "{0}/{1}");
            ctrl.evolveFormat = S(p, "evolveFormat", "{0}");

            ctrl.shieldIntactTitle = S(p, "shieldIntactTitle");
            ctrl.shieldIntactBody = S(p, "shieldIntactBody");
            ctrl.shieldBrokenTitle = S(p, "shieldBrokenTitle");
            ctrl.shieldBrokenBodyFormat = S(p, "shieldBrokenBodyFormat", "{0}");
            ctrl.surgeTitle = S(p, "surgeTitle");
            ctrl.surgeBodyFormat = S(p, "surgeBodyFormat", "{0}");

            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// La carte d'unite, en bas au centre - phases 3 et 4.
        ///
        /// Un seul objet pour les deux phases : la maquette impose la meme grammaire
        /// des deux cotes, seule la couleur change. Le joueur apprend a lire en
        /// phase 3 et n'a plus rien a apprendre en phase 4.
        /// </summary>
        private static void BuildUnitCard(RectTransform root, Canvas canvas, JObject labels)
        {
            JObject u = labels["unit"] as JObject;

            const float W = 680f;
            const float Pad = 26f;
            const float Inner = W - Pad * 2f;

            RectTransform card = NewRect("CarteUnite", root);
            // Au-dessus du bouton de fin de tour, qui occupe y 54 a 126.
            AnchorBottomCenter(card, new Vector2(0f, 150f), new Vector2(W, 268f));

            Image bg = card.gameObject.AddComponent<Image>();
            bg.color = ColPanel;
            bg.raycastTarget = false;

            UnityEngine.UI.Outline frame = card.gameObject.AddComponent<UnityEngine.UI.Outline>();
            frame.effectColor = ColLine;
            frame.effectDistance = new Vector2(1f, 1f);

            Image accent = NewImage("Liseret", card, ColCyan);
            AnchorTopRight(accent.rectTransform, Vector2.zero, new Vector2(W, 2f));

            // --- entete ---
            TextMeshProUGUI title = NewText("Nom", card, "", 22f, ColText,
                                            TextAlignmentOptions.TopRight, true);
            title.fontStyle = FontStyles.Bold;
            AnchorTopRight(title.rectTransform, new Vector2(-Pad, -18f), new Vector2(300f, 40f));

            TextMeshProUGUI subtitle = NewText("SousTitre", card, "", 16f, ColCyan,
                                               TextAlignmentOptions.TopLeft, true);
            AnchorTopLeft(subtitle.rectTransform, new Vector2(Pad, -22f), new Vector2(280f, 34f));

            TextMeshProUGUI hp = NewText("PV", card, "", 15f, ColText,
                                          TextAlignmentOptions.TopRight, false);
            hp.fontStyle = FontStyles.Bold;
            AnchorTopRight(hp.rectTransform, new Vector2(-Pad, -64f), new Vector2(100f, 30f));

            Image track = NewImage("Jauge_Fond", card, new Color32(0x7E, 0xA0, 0xFF, 0x21));
            AnchorTopRight(track.rectTransform, new Vector2(-(Pad + 110f), -70f),
                           new Vector2(Inner - 110f, 7f));

            Image hpFill = NewImage("Jauge", track.rectTransform, ColCyan);
            Stretch(hpFill.rectTransform);
            hpFill.type = Image.Type.Filled;
            hpFill.fillMethod = Image.FillMethod.Horizontal;
            hpFill.fillOrigin = (int)Image.OriginHorizontal.Right;
            hpFill.fillAmount = 1f;

            // --- quatre lignes ---
            // En hebreu la ligne se lit de droite a gauche : le libelle ouvre a
            // DROITE, puis vient le nombre, puis l'unite tout a gauche.
            TextMeshProUGUI[] rowLabels = new TextMeshProUGUI[4];
            TextMeshProUGUI[] rowValues = new TextMeshProUGUI[4];
            TextMeshProUGUI[] rowUnits = new TextMeshProUGUI[4];

            for (int i = 0; i < 4; i++)
            {
                float y = -104f - i * 38f;

                TextMeshProUGUI label = NewText("Ligne" + i + "_Libelle", card, "", 14f, ColDim,
                                                TextAlignmentOptions.TopRight, true);
                AnchorTopRight(label.rectTransform, new Vector2(-Pad, y), new Vector2(240f, 32f));

                TextMeshProUGUI value = NewText("Ligne" + i + "_Valeur", card, "", 15f, ColText,
                                                TextAlignmentOptions.TopLeft, false);
                AnchorTopLeft(value.rectTransform, new Vector2(Pad + 168f, y), new Vector2(180f, 32f));

                TextMeshProUGUI unit = NewText("Ligne" + i + "_Unite", card, "", 13f, ColDim,
                                               TextAlignmentOptions.TopLeft, true);
                AnchorTopLeft(unit.rectTransform, new Vector2(Pad, y), new Vector2(160f, 32f));

                rowLabels[i] = label;
                rowValues[i] = value;
                rowUnits[i] = unit;
            }

            // --- le chiffre flottant, au-dessus de la carte ---
            // Il est volontairement HORS de la carte : c'est l'impact, pas une donnee
            // de fiche. Le joueur doit le voir sans avoir a lire.
            RectTransform flash = NewRect("Impact", root);
            AnchorBottomCenter(flash, new Vector2(0f, 452f), new Vector2(420f, 120f));

            TextMeshProUGUI flashValue = NewText("Valeur", flash, "", 54f, ColCyan,
                                                 TextAlignmentOptions.Top, false);
            flashValue.fontStyle = FontStyles.Bold;
            AnchorTopCenter(flashValue.rectTransform, Vector2.zero, new Vector2(420f, 80f));

            TextMeshProUGUI flashCaption = NewText("Legende", flash, "", 15f, ColDim,
                                                   TextAlignmentOptions.Top, true);
            AnchorTopCenter(flashCaption.rectTransform, new Vector2(0f, -84f), new Vector2(420f, 34f));

            // --- cablage ---
            UnitActionCard ctrl = canvas.gameObject.GetComponent<UnitActionCard>();
            if (ctrl == null) ctrl = canvas.gameObject.AddComponent<UnitActionCard>();

            ctrl.panelRoot = card.gameObject;
            ctrl.accentBar = accent;
            ctrl.titleText = title;
            ctrl.subtitleText = subtitle;
            ctrl.hpText = hp;
            ctrl.hpFill = hpFill;
            ctrl.rowLabels = rowLabels;
            ctrl.rowValues = rowValues;
            ctrl.rowUnits = rowUnits;
            ctrl.flashRoot = flash.gameObject;
            ctrl.flashValueText = flashValue;
            ctrl.flashCaptionText = flashCaption;

            ctrl.tankNameFormat = S(u, "tankNameFormat", "{0}");
            ctrl.enemyName = S(u, "enemyName");
            ctrl.levelFormat = S(u, "levelFormat", "{0}");
            ctrl.hpFormat = S(u, "hpFormat", "{0}/{1}");
            ctrl.stanceNames = ReadArray(u, "stances", 3);
            ctrl.directionNames = ReadArray(labels["portal"] as JObject, "directions", 6);

            ctrl.rowTargetLabel = S(u, "rowTarget");
            ctrl.rowDamageLabel = S(u, "rowDamage");
            ctrl.rowRangeLabel = S(u, "rowRange");
            ctrl.rowMobilityLabel = S(u, "rowMobility");
            ctrl.rowOriginLabel = S(u, "rowOrigin");

            ctrl.unitHex = S(u, "unitHex");
            ctrl.unitHexPerTurn = S(u, "unitHexPerTurn");

            ctrl.targetBase = S(u, "targetBase");
            ctrl.targetEnemy = S(u, "targetEnemy");
            ctrl.targetTank = S(u, "targetTank");

            ctrl.captionDamage = S(u, "captionDamage");
            ctrl.captionShielded = S(u, "captionShielded");
            ctrl.captionBaseHp = S(u, "captionBaseHp");
            ctrl.captionMove = S(u, "captionMove");
            ctrl.damageReducedFormat = S(u, "damageReducedFormat", "{0} {1}");

            flash.gameObject.SetActive(false);
            card.gameObject.SetActive(false);
        }

        /// <summary>
        /// Le panneau de question - phase 1.
        ///
        /// Plein cadre et opaque, volontairement. C'est le seul moment ou il n'y a
        /// qu'un objet a l'ecran et qu'une decision a prendre ; laisser le plateau
        /// visible derriere ne ferait que disperser le regard. Le voile capte aussi
        /// les clics, pour qu'aucun ne traverse vers une case.
        /// </summary>
        private static void BuildTrivia(RectTransform root, Canvas canvas, JObject labels)
        {
            JObject t = labels["trivia"] as JObject;

            const float ColWidth = 940f;

            RectTransform panel = NewRect("PanneauQuestion", root);
            Stretch(panel);

            Image dim = panel.gameObject.AddComponent<Image>();
            dim.color = new Color32(0x03, 0x05, 0x0B, 0xDB);
            dim.raycastTarget = true;     // le voile absorbe les clics

            // --- categorie, a droite : en hebreu, la droite ouvre la lecture ---
            TextMeshProUGUI category = NewText("Categorie", panel, "", 44f, ColText,
                                               TextAlignmentOptions.TopRight, true);
            category.fontStyle = FontStyles.Bold;
            AnchorCenterRight(category.rectTransform, new Vector2(ColWidth * 0.5f, 296f),
                              new Vector2(520f, 58f));

            // --- le gain, annonce AVANT la reponse ---
            TextMeshProUGUI rewardLabel = NewText("Tarif_Libelle", panel, S(t, "rewardLabel"),
                                                  18f, ColDim, TextAlignmentOptions.TopLeft, true);
            AnchorCenterLeft(rewardLabel.rectTransform, new Vector2(-ColWidth * 0.5f, 314f),
                             new Vector2(220f, 26f));

            TextMeshProUGUI rewardValue = NewText("Tarif_Valeur", panel, "", 36f, ColGold,
                                                  TextAlignmentOptions.TopLeft, false);
            rewardValue.fontStyle = FontStyles.Bold;
            AnchorCenterLeft(rewardValue.rectTransform, new Vector2(-ColWidth * 0.5f, 280f),
                             new Vector2(220f, 46f));

            // --- l'enonce : l'objet principal de l'ecran ---
            TextMeshProUGUI question = NewText("Question", panel, "", 46f, ColText,
                                               TextAlignmentOptions.TopRight, true);
            AnchorCenter(question.rectTransform, new Vector2(0f, 150f), new Vector2(ColWidth, 200f));
            question.enableWordWrapping = true;

            // --- les quatre reponses ---
            Button[] buttons = new Button[4];
            TextMeshProUGUI[] answerTexts = new TextMeshProUGUI[4];
            Image[] answerBgs = new Image[4];

            TriviaPanelController ctrl = canvas.gameObject.GetComponent<TriviaPanelController>();
            if (ctrl == null) ctrl = canvas.gameObject.AddComponent<TriviaPanelController>();

            for (int i = 0; i < 4; i++)
            {
                float y = -20f - i * 106f;

                RectTransform row = NewRect("Reponse" + i, panel);
                AnchorCenter(row, new Vector2(0f, y), new Vector2(ColWidth, 92f));

                Image rowBg = row.gameObject.AddComponent<Image>();
                rowBg.color = new Color32(0x7E, 0xA0, 0xFF, 0x10);

                UnityEngine.UI.Outline rowLine = row.gameObject.AddComponent<UnityEngine.UI.Outline>();
                rowLine.effectColor = ColLine;
                rowLine.effectDistance = new Vector2(1f, 1f);

                Button button = row.gameObject.AddComponent<Button>();
                button.targetGraphic = rowBg;
                // Transition None : le survol est gere par TriviaPanelController, qui
                // teinte le fond. Laisser aussi la transition par defaut ferait deux
                // effets concurrents sur la meme image.
                button.transition = Selectable.Transition.None;

                TriviaAnswerHover hover = row.gameObject.AddComponent<TriviaAnswerHover>();
                hover.panel = ctrl;
                hover.index = i;

                TextMeshProUGUI text = NewText("Texte", row, "", 26f, ColText,
                                               TextAlignmentOptions.Right, true);
                AnchorTopRight(text.rectTransform, new Vector2(-34f, -24f), new Vector2(860f, 46f));

                buttons[i] = button;
                answerTexts[i] = text;
                answerBgs[i] = rowBg;
            }

            // --- le chrono ---
            TextMeshProUGUI timerLabel = NewText("Chrono_Libelle", panel, S(t, "timerLabel"),
                                                 18f, ColDim, TextAlignmentOptions.TopRight, true);
            AnchorCenterRight(timerLabel.rectTransform, new Vector2(ColWidth * 0.5f, -432f),
                              new Vector2(340f, 36f));

            TextMeshProUGUI timerValue = NewText("Chrono_Valeur", panel, "", 22f, ColCyan,
                                                 TextAlignmentOptions.TopLeft, true);
            timerValue.fontStyle = FontStyles.Bold;
            AnchorCenterLeft(timerValue.rectTransform, new Vector2(-ColWidth * 0.5f, -432f),
                             new Vector2(260f, 38f));

            Image timerTrack = NewImage("Chrono_Fond", panel, new Color32(0x7E, 0xA0, 0xFF, 0x1E));
            AnchorCenter(timerTrack.rectTransform, new Vector2(0f, -472f), new Vector2(ColWidth, 5f));

            Image timerFill = NewImage("Chrono_Jauge", timerTrack.rectTransform, ColCyan);
            Stretch(timerFill.rectTransform);
            timerFill.type = Image.Type.Filled;
            timerFill.fillMethod = Image.FillMethod.Horizontal;
            // En RTL le temps s'ecoule vers la gauche : la jauge se vide depuis la gauche.
            timerFill.fillOrigin = (int)Image.OriginHorizontal.Right;
            timerFill.fillAmount = 1f;

            // --- cablage ---
            ctrl.panelRoot = panel.gameObject;
            ctrl.dimmer = dim;
            ctrl.categoryText = category;
            ctrl.rewardLabelText = rewardLabel;
            ctrl.rewardValueText = rewardValue;
            ctrl.questionText = question;
            ctrl.answerButtons = buttons;
            ctrl.answerTexts = answerTexts;
            ctrl.answerBackgrounds = answerBgs;
            ctrl.timerLabelText = timerLabel;
            ctrl.timerValueText = timerValue;
            ctrl.timerFill = timerFill;

            ctrl.rewardLabel = S(t, "rewardLabel");
            ctrl.timerLabel = S(t, "timerLabel");
            ctrl.rewardFormat = S(t, "rewardFormat", "+{0}");
            ctrl.secondsFormat = S(t, "secondsFormat", "{0}");

            // Les categories : deux tableaux paralleles, dans le meme ordre.
            JObject cats = (t != null) ? t["categories"] as JObject : null;
            if (cats != null)
            {
                int count = cats.Count;
                string[] keys = new string[count];
                string[] names = new string[count];

                int k = 0;
                foreach (System.Collections.Generic.KeyValuePair<string, JToken> pair in cats)
                {
                    keys[k] = pair.Key;
                    names[k] = (pair.Value != null) ? pair.Value.ToString() : "";
                    k++;
                }

                ctrl.categoryKeys = keys;
                ctrl.categoryNames = names;
            }

            // On branche le panneau sur le TriviaManager de la scene, s'il existe :
            // sans ce lien, le jeu continuerait d'ouvrir l'ancienne modale Reach.
            // Objets desactives compris : le TriviaManager peut vivre sur un canvas
            // qu'on vient d'eteindre en attendant de le supprimer.
            TriviaManager trivia = Object.FindFirstObjectByType<TriviaManager>(FindObjectsInactive.Include);
            if (trivia != null)
            {
                trivia.panel = ctrl;

                // Le verdict doit rester lisible : trop court, le joueur ne sait pas
                // s'il avait raison, et c'est la seule chose qu'il a gagnee en repondant.
                trivia.resultHoldSeconds = 1.7f;

                EditorUtility.SetDirty(trivia);
                Debug.Log("[HudBuilder] Le panneau de question est branche sur le TriviaManager de la scene.");
            }
            else
            {
                Debug.LogWarning("[HudBuilder] Aucun TriviaManager dans la scene : glisse le panneau "
                               + "dans son champ \"panel\" quand il sera la.");
            }

            Enlarge(panel, 1.2f);

            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// L'ecran de choix du sujet - la premiere decision de chaque tour.
        ///
        /// Quatre cartes cote a cote, lues de droite a gauche. Chacune porte le nom du
        /// sujet, son palier de difficulte en pastilles, et le gain que ce palier
        /// commande. Le gain n'est pas decoratif : c'est celui de la question deja
        /// tiree pour ce sujet.
        /// </summary>
        private static void BuildSubjectChoice(RectTransform root, Canvas canvas, JObject labels)
        {
            JObject sc = labels["subjectChoice"] as JObject;
            JObject tr = labels["trivia"] as JObject;

            const float CardW = 340f;
            const float CardH = 340f;
            const float Gap = 26f;

            RectTransform panel = NewRect("ChoixDuSujet", root);
            Stretch(panel);

            Image dim = panel.gameObject.AddComponent<Image>();
            dim.color = new Color32(0x03, 0x05, 0x0B, 0xDB);
            dim.raycastTarget = true;

            TextMeshProUGUI title = NewText("Titre", panel, S(sc, "title"), 40f, ColText,
                                            TextAlignmentOptions.Top, true);
            title.fontStyle = FontStyles.Bold;
            AnchorCenter(title.rectTransform, new Vector2(0f, 292f), new Vector2(1000f, 56f));

            TextMeshProUGUI subtitle = NewText("SousTitre", panel, S(sc, "subtitle"), 19f, ColDim,
                                               TextAlignmentOptions.Top, true);
            AnchorCenter(subtitle.rectTransform, new Vector2(0f, 238f), new Vector2(1000f, 32f));

            SubjectChoicePanel ctrl = canvas.gameObject.GetComponent<SubjectChoicePanel>();
            if (ctrl == null) ctrl = canvas.gameObject.AddComponent<SubjectChoicePanel>();

            Button[] buttons = new Button[4];
            Image[] backgrounds = new Image[4];
            Image[] accents = new Image[4];
            TextMeshProUGUI[] names = new TextMeshProUGUI[4];
            TextMeshProUGUI[] rewards = new TextMeshProUGUI[4];
            TextMeshProUGUI[] tiers = new TextMeshProUGUI[4];
            Image[] dots = new Image[16];

            // Quatre cartes centrees. En hebreu la lecture ouvre a DROITE : le premier
            // sujet est la carte la plus a droite, d'ou le signe du decalage.
            float span = CardW * 4f + Gap * 3f;
            float firstCenter = span * 0.5f - CardW * 0.5f;

            for (int i = 0; i < 4; i++)
            {
                float x = firstCenter - i * (CardW + Gap);

                RectTransform card = NewRect("Sujet" + i, panel);
                AnchorCenter(card, new Vector2(x, -30f), new Vector2(CardW, CardH));

                Image bg = card.gameObject.AddComponent<Image>();
                bg.color = new Color32(0x7E, 0xA0, 0xFF, 0x10);

                UnityEngine.UI.Outline edge = card.gameObject.AddComponent<UnityEngine.UI.Outline>();
                edge.effectColor = ColLine;
                edge.effectDistance = new Vector2(1f, 1f);

                Button button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = bg;
                // Le survol est teinte par SubjectChoicePanel : deux effets concurrents
                // sur la meme image se combattraient.
                button.transition = Selectable.Transition.None;

                SubjectCardHover hover = card.gameObject.AddComponent<SubjectCardHover>();
                hover.panel = ctrl;
                hover.slot = i;

                Image accent = NewImage("Liseret", card, ColCyan);
                AnchorTopRight(accent.rectTransform, Vector2.zero, new Vector2(CardW, 3f));

                TextMeshProUGUI name = NewText("Nom", card, "", 32f, ColText,
                                               TextAlignmentOptions.Top, true);
                name.fontStyle = FontStyles.Bold;
                AnchorTopCenter(name.rectTransform, new Vector2(0f, -32f), new Vector2(CardW - 24f, 58f));

                // Les pastilles : le gain dit COMBIEN, les pastilles disent POURQUOI.
                // Sans elles le joueur croit a un tirage au sort, alors que c'est une
                // echelle de difficulte.
                RectTransform dotRow = NewRect("Paliers", card);
                AnchorTopCenter(dotRow, new Vector2(0f, -104f), new Vector2(140f, 11f));

                HorizontalLayoutGroup dotLayout = dotRow.gameObject.AddComponent<HorizontalLayoutGroup>();
                dotLayout.spacing = 7f;
                dotLayout.childAlignment = TextAnchor.MiddleCenter;
                dotLayout.childControlWidth = true;
                dotLayout.childControlHeight = true;
                dotLayout.childForceExpandWidth = true;
                dotLayout.childForceExpandHeight = true;
                dotLayout.reverseArrangement = true;

                for (int d = 0; d < 4; d++)
                    dots[i * 4 + d] = NewImage("Palier" + d, dotRow, ColLine);

                TextMeshProUGUI tier = NewText("Difficulte", card, "", 16f, ColDim,
                                               TextAlignmentOptions.Top, true);
                AnchorTopCenter(tier.rectTransform, new Vector2(0f, -132f), new Vector2(CardW - 24f, 34f));

                TextMeshProUGUI reward = NewText("Gain", card, "", 58f, ColGold,
                                                 TextAlignmentOptions.Top, false);
                reward.fontStyle = FontStyles.Bold;
                AnchorTopCenter(reward.rectTransform, new Vector2(0f, -196f), new Vector2(CardW - 24f, 100f));

                buttons[i] = button;
                backgrounds[i] = bg;
                accents[i] = accent;
                names[i] = name;
                rewards[i] = reward;
                tiers[i] = tier;
            }

            // --- cablage ---
            ctrl.panelRoot = panel.gameObject;
            ctrl.titleText = title;
            ctrl.subtitleText = subtitle;
            ctrl.cardButtons = buttons;
            ctrl.cardBackgrounds = backgrounds;
            ctrl.cardAccents = accents;
            ctrl.cardNames = names;
            ctrl.cardRewards = rewards;
            ctrl.cardDifficultyLabels = tiers;
            ctrl.cardDots = dots;

            ctrl.title = S(sc, "title");
            ctrl.subtitle = S(sc, "subtitle");
            ctrl.rewardFormat = S(sc, "rewardFormat", "+{0}");
            ctrl.difficultyNames = ReadArray(sc, "difficulties", 4);

            // Les noms des sujets viennent de trivia.categories : une seule traduction,
            // au meme endroit, partagee avec le panneau de question.
            JObject cats = (tr != null) ? tr["categories"] as JObject : null;
            if (cats != null)
            {
                int count = cats.Count;
                string[] keys = new string[count];
                string[] display = new string[count];

                int k = 0;
                foreach (System.Collections.Generic.KeyValuePair<string, JToken> pair in cats)
                {
                    keys[k] = pair.Key;
                    display[k] = (pair.Value != null) ? pair.Value.ToString() : "";
                    k++;
                }

                ctrl.categoryKeys = keys;
                ctrl.categoryNames = display;
            }

            TriviaManager trivia = Object.FindFirstObjectByType<TriviaManager>(FindObjectsInactive.Include);
            if (trivia != null)
            {
                trivia.subjectPanel = ctrl;
                EditorUtility.SetDirty(trivia);
            }

            Enlarge(panel, 1.2f);

            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// L'ecran de choix d'un Tank : ses trois postures cote a cote, plus
        /// l'evolution en quatrieme carte quand elle est possible.
        ///
        /// POURQUOI UN GROUPE HORIZONTAL ET PAS QUATRE POSITIONS FIXES
        ///
        /// Cet ecran s'ouvre tantot avec quatre cartes, tantot avec trois - a la
        /// creation il n'y a rien a faire evoluer. A positions fixes, les trois cartes
        /// restantes resteraient collees a droite avec un trou a gauche. Le groupe
        /// horizontal ignore un enfant desactive et recentre le reste tout seul.
        ///
        /// reverseArrangement place la premiere carte a DROITE : en hebreu la lecture
        /// ouvre de ce cote, et la premiere posture doit etre la premiere lue.
        /// </summary>
        private static void BuildTankChoice(RectTransform root, Canvas canvas, JObject labels)
        {
            JObject tc = labels["tankChoice"] as JObject;
            JObject un = labels["unit"] as JObject;

            const float CardW = 330f;
            const float CardH = 420f;
            const float Gap = 24f;

            RectTransform panel = NewRect("ChoixDuTank", root);
            Stretch(panel);

            Image dim = panel.gameObject.AddComponent<Image>();
            dim.color = new Color32(0x03, 0x05, 0x0B, 0xDB);
            // Le voile absorbe les clics : pendant le choix, rien ne doit atteindre le
            // plateau, sinon un clic a cote poserait un second Tank.
            dim.raycastTarget = true;

            TextMeshProUGUI title = NewText("Titre", panel, S(tc, "createTitle"), 40f, ColText,
                                            TextAlignmentOptions.Top, true);
            title.fontStyle = FontStyles.Bold;
            AnchorCenter(title.rectTransform, new Vector2(0f, 300f), new Vector2(1000f, 56f));

            TextMeshProUGUI subtitle = NewText("SousTitre", panel, S(tc, "createSubtitle"), 19f, ColDim,
                                               TextAlignmentOptions.Top, true);
            AnchorCenter(subtitle.rectTransform, new Vector2(0f, 250f), new Vector2(1100f, 32f));

            TankChoicePanel ctrl = canvas.gameObject.GetComponent<TankChoicePanel>();
            if (ctrl == null) ctrl = canvas.gameObject.AddComponent<TankChoicePanel>();

            // --- la rangee de cartes ---
            RectTransform row = NewRect("Cartes", panel);
            AnchorCenter(row, new Vector2(0f, -10f), new Vector2(1600f, CardH));

            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = Gap;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.reverseArrangement = true;

            int slots = TankChoicePanel.MaxOptions;

            Button[] buttons = new Button[slots];
            Image[] backgrounds = new Image[slots];
            Image[] accents = new Image[slots];
            Image[] icons = new Image[slots];
            TextMeshProUGUI[] names = new TextMeshProUGUI[slots];
            TextMeshProUGUI[] bodies = new TextMeshProUGUI[slots];
            TextMeshProUGUI[] costs = new TextMeshProUGUI[slots];

            for (int i = 0; i < slots; i++)
            {
                RectTransform card = NewRect("Option" + i, row);
                card.sizeDelta = new Vector2(CardW, CardH);

                LayoutElement size = card.gameObject.AddComponent<LayoutElement>();
                size.preferredWidth = CardW;
                size.preferredHeight = CardH;
                size.flexibleWidth = 0f;
                size.flexibleHeight = 0f;

                Image bg = card.gameObject.AddComponent<Image>();
                bg.color = new Color32(0x7E, 0xA0, 0xFF, 0x10);

                UnityEngine.UI.Outline edge = card.gameObject.AddComponent<UnityEngine.UI.Outline>();
                edge.effectColor = ColLine;
                edge.effectDistance = new Vector2(1f, 1f);

                Button button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = bg;
                // Le survol est teinte par TankChoicePanel : deux effets concurrents sur
                // la meme image se combattraient.
                button.transition = Selectable.Transition.None;

                TankCardHover hover = card.gameObject.AddComponent<TankCardHover>();
                hover.panel = ctrl;
                hover.slot = i;

                Image accent = NewImage("Liseret", card, ColCyan);
                AnchorTopRight(accent.rectTransform, Vector2.zero, new Vector2(CardW, 3f));

                // Le pictogramme, teinte par TankChoicePanel avec la couleur de la
                // posture. Trois chevrons de couleurs differentes se distinguent plus
                // vite que trois mots hebreux, surtout quand on joue vite.
                Image cardIcon = NewImage("Icone", card, ColText);
                AnchorTopCenter(cardIcon.rectTransform, new Vector2(0f, -22f), new Vector2(44f, 44f));
                cardIcon.preserveAspect = true;
                Bind(cardIcon, (i >= 3) ? MNLTHII.UI.IconKind.Crystal : MNLTHII.UI.IconKind.Tank);

                TextMeshProUGUI name = NewText("Nom", card, "", 30f, ColText,
                                               TextAlignmentOptions.Top, true);
                name.fontStyle = FontStyles.Bold;
                AnchorTopCenter(name.rectTransform, new Vector2(0f, -74f), new Vector2(CardW - 24f, 54f));

                Image rule = NewImage("Filet", card, ColLine);
                AnchorTopCenter(rule.rectTransform, new Vector2(0f, -136f), new Vector2(CardW - 72f, 1f));

                // Le texte qui dit ce que la posture FAIT. C'est la seule raison d'etre
                // de cet ecran : sans lui, choisir entre trois mots hebreux serait un
                // tirage au sort.
                TextMeshProUGUI body = NewText("Explication", card, "", 19f, ColDim,
                                               TextAlignmentOptions.Top, true);
                body.enableWordWrapping = true;
                body.lineSpacing = 8f;
                AnchorTopCenter(body.rectTransform, new Vector2(0f, -152f), new Vector2(CardW - 40f, 160f));

                // Le prix n'est PAS en RTL : un nombre dans un champ RTL voit ses
                // chiffres s'inverser, et 15 devient 51.
                TextMeshProUGUI cost = NewText("Cout", card, "", 40f, ColGold,
                                               TextAlignmentOptions.Bottom, false);
                cost.fontStyle = FontStyles.Bold;
                AnchorBottomCenter(cost.rectTransform, new Vector2(0f, 26f), new Vector2(CardW - 24f, 58f));

                buttons[i] = button;
                backgrounds[i] = bg;
                accents[i] = accent;
                icons[i] = cardIcon;
                names[i] = name;
                bodies[i] = body;
                costs[i] = cost;
            }

            // --- annuler ---
            RectTransform cancel = NewRect("Annuler", panel);
            AnchorCenter(cancel, new Vector2(0f, -268f), new Vector2(280f, 62f));

            Image cancelBg = cancel.gameObject.AddComponent<Image>();
            cancelBg.color = new Color32(0x7E, 0xA0, 0xFF, 0x0C);

            UnityEngine.UI.Outline cancelEdge = cancel.gameObject.AddComponent<UnityEngine.UI.Outline>();
            cancelEdge.effectColor = ColLine;
            cancelEdge.effectDistance = new Vector2(1f, 1f);

            Button cancelButton = cancel.gameObject.AddComponent<Button>();
            cancelButton.targetGraphic = cancelBg;

            TextMeshProUGUI cancelText = NewText("Libelle", cancel, S(tc, "cancel"), 22f, ColDim,
                                                 TextAlignmentOptions.Center, true);
            Stretch(cancelText.rectTransform);

            // --- vue depuis le tank (seulement pour un Tank deja pose) ---
            // Sous "annuler", en cyan : ce n'est pas un choix de posture, c'est une
            // autre facon de regarder. Le clic est cable par TankChoicePanel au
            // lancement, jamais ici.
            JObject im = labels["immersive"] as JObject;

            RectTransform view = NewRect("VueTank", panel);
            AnchorCenter(view, new Vector2(0f, -346f), new Vector2(340f, 62f));

            Image viewBg = view.gameObject.AddComponent<Image>();
            viewBg.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.10f);

            UnityEngine.UI.Outline viewEdge = view.gameObject.AddComponent<UnityEngine.UI.Outline>();
            viewEdge.effectColor = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.55f);
            viewEdge.effectDistance = new Vector2(1f, 1f);

            Button viewButton = view.gameObject.AddComponent<Button>();
            viewButton.targetGraphic = viewBg;

            TextMeshProUGUI viewText = NewText("Libelle", view, S(im, "viewButton"), 22f, ColCyan,
                                               TextAlignmentOptions.Center, true);
            Stretch(viewText.rectTransform);
            viewText.fontStyle = FontStyles.Bold;

            view.gameObject.SetActive(false);

            // --- cablage ---
            ctrl.panelRoot = panel.gameObject;
            ctrl.titleText = title;
            ctrl.subtitleText = subtitle;
            ctrl.cardButtons = buttons;
            ctrl.cardBackgrounds = backgrounds;
            ctrl.cardAccents = accents;
            ctrl.cardIcons = icons;
            ctrl.cardNames = names;
            ctrl.cardBodies = bodies;
            ctrl.cardCosts = costs;
            ctrl.cancelButton = cancelButton;
            ctrl.cancelText = cancelText;
            ctrl.viewButton = viewButton;
            ctrl.viewText = viewText;
            ctrl.viewLabel = S(im, "viewButton");

            ctrl.createTitle = S(tc, "createTitle");
            ctrl.createSubtitle = S(tc, "createSubtitle");
            ctrl.changeTitle = S(tc, "changeTitle");
            ctrl.changeSubtitle = S(tc, "changeSubtitle");
            ctrl.cancelLabel = S(tc, "cancel");
            ctrl.freeLabel = S(tc, "free");
            ctrl.currentLabel = S(tc, "current");
            ctrl.evolveName = S(tc, "evolveName");
            ctrl.evolveBody = S(tc, "evolveBody");
            ctrl.crystalOrderName = S(tc, "crystalName");
            ctrl.crystalOrderBody = S(tc, "crystalBody");
            ctrl.crystalOrderCurrent = S(tc, "crystalCurrent");

            // Les noms des postures viennent de unit.stances : la carte d'unite et cet
            // ecran doivent dire le meme mot pour la meme chose.
            ctrl.stanceNames = ReadArray(un, "stances", 3);
            ctrl.stanceBodies = ReadArray(tc, "bodies", 3);

            ctrl.cardIdleColor = Color.white;
            ctrl.cardHoverColor = new Color(1.25f, 1.35f, 1.5f, 1f);
            Enlarge(panel, 1.08f);

            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// LE CONSEILLER : au plus trois cartes, en bas a droite, pendant la phase de
        /// depense.
        ///
        /// POURQUOI UN GROUPE VERTICAL AVEC AJUSTEMENT DE TAILLE
        ///
        /// Le nombre de conseils varie d'un tour a l'autre : trois quand la situation
        /// est tendue, un seul quand tout va bien, zero quand il n'y a rien d'utile a
        /// dire. A hauteur fixe, un seul conseil laisserait deux trous et le panneau
        /// aurait l'air casse. Le groupe vertical ignore une carte desactivee et
        /// l'ajusteur ramene le fond a la hauteur reellement occupee.
        ///
        /// Le pivot est en bas a droite : le panneau grandit donc VERS LE HAUT, et son
        /// bord bas ne bouge jamais - l'oeil retrouve les cartes au meme endroit.
        /// </summary>
        private static void BuildAdvisor(RectTransform root, Canvas canvas, JObject labels)
        {
            JObject ad = labels["advisor"] as JObject;
            JObject un = labels["unit"] as JObject;

            const float PanelW = 470f;
            const float CardW = 438f;
            const float CardH = 104f;

            RectTransform panel = NewRect("Conseiller", root);
            panel.anchorMin = new Vector2(1f, 0f);
            panel.anchorMax = new Vector2(1f, 0f);
            panel.pivot = new Vector2(1f, 0f);
            panel.anchoredPosition = new Vector2(-56f, 40f);
            panel.sizeDelta = new Vector2(PanelW, 400f);

            Image bg = panel.gameObject.AddComponent<Image>();
            bg.color = ColPanel;
            bg.raycastTarget = false;

            UnityEngine.UI.Outline frame = panel.gameObject.AddComponent<UnityEngine.UI.Outline>();
            frame.effectColor = ColLine;
            frame.effectDistance = new Vector2(1f, 1f);

            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 14, 16);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AdvisorPanel ctrl = canvas.gameObject.GetComponent<AdvisorPanel>();
            if (ctrl == null) ctrl = canvas.gameObject.AddComponent<AdvisorPanel>();

            // --- entete ---
            TextMeshProUGUI header = NewText("Entete", panel, S(ad, "header"), 18f, ColCyan,
                                             TextAlignmentOptions.TopRight, true);
            header.fontStyle = FontStyles.Bold;
            header.rectTransform.sizeDelta = new Vector2(CardW, 30f);

            LayoutElement headerSize = header.gameObject.AddComponent<LayoutElement>();
            headerSize.preferredWidth = CardW;
            headerSize.preferredHeight = 30f;
            headerSize.flexibleWidth = 0f;
            headerSize.flexibleHeight = 0f;

            int slots = AdvisorPanel.MaxCards;

            Button[] buttons = new Button[slots];
            GameObject[] roots = new GameObject[slots];
            Image[] backgrounds = new Image[slots];
            Image[] accents = new Image[slots];
            TextMeshProUGUI[] titles = new TextMeshProUGUI[slots];
            TextMeshProUGUI[] reasons = new TextMeshProUGUI[slots];
            TextMeshProUGUI[] costs = new TextMeshProUGUI[slots];

            for (int i = 0; i < slots; i++)
            {
                RectTransform card = NewRect("Conseil" + i, panel);
                card.sizeDelta = new Vector2(CardW, CardH);

                LayoutElement size = card.gameObject.AddComponent<LayoutElement>();
                size.preferredWidth = CardW;
                size.preferredHeight = CardH;
                size.flexibleWidth = 0f;
                size.flexibleHeight = 0f;

                Image cardBg = card.gameObject.AddComponent<Image>();
                cardBg.color = new Color32(0x7E, 0xA0, 0xFF, 0x10);

                Button button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = cardBg;
                // Le survol est teinte par AdvisorPanel : deux effets concurrents sur
                // la meme image se combattraient.
                button.transition = Selectable.Transition.None;

                AdvisorCardHover hover = card.gameObject.AddComponent<AdvisorCardHover>();
                hover.panel = ctrl;
                hover.slot = i;

                // Le liseret porte l'urgence : rouge quand il faut agir ce tour-ci,
                // couleur de posture sinon. C'est lisible avant d'avoir lu un mot.
                Image accent = NewImage("Liseret", card, ColCyan);
                AnchorTopRight(accent.rectTransform, Vector2.zero, new Vector2(CardW, 3f));

                TextMeshProUGUI title = NewText("Titre", card, "", 21f, ColText,
                                                TextAlignmentOptions.TopRight, true);
                title.fontStyle = FontStyles.Bold;
                AnchorTopRight(title.rectTransform, new Vector2(-14f, -12f), new Vector2(280f, 34f));

                // Le prix n'est PAS en RTL : un nombre dans un champ RTL voit ses
                // chiffres s'inverser, et 15 devient 51.
                TextMeshProUGUI cost = NewText("Cout", card, "", 22f, ColGold,
                                               TextAlignmentOptions.TopLeft, false);
                cost.fontStyle = FontStyles.Bold;
                AnchorTopLeft(cost.rectTransform, new Vector2(14f, -12f), new Vector2(110f, 34f));

                // La raison est le coeur de la carte : sans elle, le conseiller dit quoi
                // faire mais le joueur n'apprend jamais pourquoi, et il en depend a vie.
                TextMeshProUGUI reason = NewText("Raison", card, "", 15f, ColDim,
                                                 TextAlignmentOptions.TopRight, true);
                reason.enableWordWrapping = true;
                reason.lineSpacing = 6f;
                AnchorTopRight(reason.rectTransform, new Vector2(-14f, -50f), new Vector2(CardW - 28f, 46f));

                buttons[i] = button;
                roots[i] = card.gameObject;
                backgrounds[i] = cardBg;
                accents[i] = accent;
                titles[i] = title;
                reasons[i] = reason;
                costs[i] = cost;
            }

            // --- cablage ---
            ctrl.panelRoot = panel.gameObject;
            ctrl.headerText = header;
            ctrl.cardButtons = buttons;
            ctrl.cardRoots = roots;
            ctrl.cardBackgrounds = backgrounds;
            ctrl.cardAccents = accents;
            ctrl.cardTitles = titles;
            ctrl.cardReasons = reasons;
            ctrl.cardCosts = costs;

            ctrl.header = S(ad, "header");
            ctrl.titleCreateTank = S(ad, "titleCreateTank");
            ctrl.titleChangeStance = S(ad, "titleChangeStance");
            ctrl.titleEvolve = S(ad, "titleEvolve");
            ctrl.titleBunker = S(ad, "titleBunker");
            ctrl.titleCrystal = S(ad, "titleCrystal");
            ctrl.titleCommand = S(ad, "titleCommand");
            ctrl.titleBuild = S(ad, "titleBuild");

            ctrl.reasons = ReadArray(ad, "reasons", MNLTHII.Rules.AdvisorRules.REASON_COUNT);
            ctrl.stanceNames = ReadArray(un, "stances", 3);

            ctrl.cardIdleColor = Color.white;
            ctrl.cardHoverColor = new Color(1.25f, 1.35f, 1.5f, 1f);

            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// L'ECRAN D'ENSEIGNEMENT, qui remplace la question de Trivia.
        ///
        /// Ses textes ne viennent PAS de hud_labels.json mais de teachings.json, lu au
        /// demarrage par TeachingManager. La raison est simple : les enseignements et
        /// leurs libelles forment un tout, et les separer en deux fichiers garantissait
        /// qu'un jour l'un serait traduit sans l'autre. Le builder n'a donc ici aucun
        /// libelle a recopier - il pose la mise en page, et c'est tout.
        ///
        /// Trois etats occupent la meme surface : l'enseignement, la question, le
        /// resultat. Un seul est actif a la fois, ce qui evite un panneau qui grandit et
        /// retrecit entre les etapes.
        /// </summary>
        private static void BuildTeaching(RectTransform root, Canvas canvas)
        {
            RectTransform panel = NewRect("Enseignement", root);
            Stretch(panel);

            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = true;

            Image dim = panel.gameObject.AddComponent<Image>();
            dim.color = new Color32(0x03, 0x05, 0x0B, 0xEB);
            dim.raycastTarget = true;

            TeachingPanel ctrl = canvas.gameObject.GetComponent<TeachingPanel>();
            if (ctrl == null) ctrl = canvas.gameObject.AddComponent<TeachingPanel>();

            // --- entete ---
            TextMeshProUGUI header = NewText("Titre", panel, "", 34f, ColText,
                                             TextAlignmentOptions.Top, true);
            header.fontStyle = FontStyles.Bold;
            AnchorCenter(header.rectTransform, new Vector2(0f, 372f), new Vector2(1000f, 50f));

            // LA PHRASE. Elle dit pourquoi cet ecran existe, et elle ne ment pas :
            // une bonne reponse fissure vraiment un Shofar.
            TextMeshProUGUI tagline = NewText("Accroche", panel, "", 20f, ColCyan,
                                              TextAlignmentOptions.Top, true);
            AnchorCenter(tagline.rectTransform, new Vector2(0f, 326f), new Vector2(1100f, 34f));

            Image ruleTop = NewImage("Filet_Haut", panel, ColLine);
            AnchorCenter(ruleTop.rectTransform, new Vector2(0f, 292f), new Vector2(880f, 1f));

            // =====================================================
            //  1. L'enseignement
            // =====================================================
            RectTransform teaching = NewRect("Texte", panel);
            AnchorCenter(teaching, new Vector2(0f, -20f), new Vector2(1040f, 560f));

            // La source en premier, et en petit : elle donne son autorite au texte sans
            // lui voler la vedette.
            TextMeshProUGUI source = NewText("Source", teaching, "", 19f, ColGold,
                                             TextAlignmentOptions.Top, true);
            AnchorTopCenter(source.rectTransform, new Vector2(0f, -12f), new Vector2(1000f, 34f));

            TextMeshProUGUI title = NewText("Sujet", teaching, "", 40f, ColText,
                                            TextAlignmentOptions.Top, true);
            title.fontStyle = FontStyles.Bold;
            AnchorTopCenter(title.rectTransform, new Vector2(0f, -56f), new Vector2(1000f, 62f));

            TextMeshProUGUI body = NewText("Corps", teaching, "", 25f, ColText,
                                           TextAlignmentOptions.Top, true);
            body.enableWordWrapping = true;
            body.lineSpacing = 16f;
            AnchorTopCenter(body.rectTransform, new Vector2(0f, -140f), new Vector2(940f, 300f));

            // Le compteur n'est PAS en RTL : "1/2" dans un champ RTL devient "2/1".
            TextMeshProUGUI counter = NewText("Compteur", teaching, "", 18f, ColDim,
                                              TextAlignmentOptions.Bottom, false);
            AnchorBottomCenter(counter.rectTransform, new Vector2(0f, 96f), new Vector2(200f, 32f));

            RectTransform next = NewRect("Suivant", teaching);
            AnchorBottomCenter(next, new Vector2(0f, 20f), new Vector2(300f, 66f));

            Image nextBg = next.gameObject.AddComponent<Image>();
            nextBg.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.14f);

            UnityEngine.UI.Outline nextEdge = next.gameObject.AddComponent<UnityEngine.UI.Outline>();
            nextEdge.effectColor = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.55f);
            nextEdge.effectDistance = new Vector2(1.5f, 1.5f);

            Button nextButton = next.gameObject.AddComponent<Button>();
            nextButton.targetGraphic = nextBg;

            TextMeshProUGUI nextLabel = NewText("Libelle", next, "", 24f,
                                                new Color32(0xBF, 0xF6, 0xEF, 0xFF),
                                                TextAlignmentOptions.Center, true);
            Stretch(nextLabel.rectTransform);

            // =====================================================
            //  2. La question
            // =====================================================
            RectTransform question = NewRect("Question", panel);
            AnchorCenter(question, new Vector2(0f, -20f), new Vector2(1040f, 560f));

            TextMeshProUGUI questionTitle = NewText("Intitule", question, "", 24f, ColCyan,
                                                    TextAlignmentOptions.Top, true);
            questionTitle.fontStyle = FontStyles.Bold;
            AnchorTopCenter(questionTitle.rectTransform, new Vector2(0f, -8f), new Vector2(900f, 40f));

            TextMeshProUGUI situation = NewText("Situation", question, "", 26f, ColText,
                                                TextAlignmentOptions.Top, true);
            situation.enableWordWrapping = true;
            situation.lineSpacing = 14f;
            AnchorTopCenter(situation.rectTransform, new Vector2(0f, -62f), new Vector2(920f, 170f));

            Button[] answerButtons = new Button[TeachingPanel.MaxAnswers];
            Image[] answerBackgrounds = new Image[TeachingPanel.MaxAnswers];
            TextMeshProUGUI[] answerTexts = new TextMeshProUGUI[TeachingPanel.MaxAnswers];

            const float AnswerW = 880f;
            const float AnswerH = 86f;

            for (int i = 0; i < TeachingPanel.MaxAnswers; i++)
            {
                RectTransform card = NewRect("Reponse" + i, question);
                AnchorTopCenter(card, new Vector2(0f, -252f - i * (AnswerH + 14f)),
                                new Vector2(AnswerW, AnswerH));

                Image bg = card.gameObject.AddComponent<Image>();
                bg.color = new Color32(0x7E, 0xA0, 0xFF, 0x12);

                UnityEngine.UI.Outline edge = card.gameObject.AddComponent<UnityEngine.UI.Outline>();
                edge.effectColor = ColLine;
                edge.effectDistance = new Vector2(1f, 1f);

                Button button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = bg;
                // Le survol est teinte par TeachingPanel : deux effets concurrents sur
                // la meme image se combattraient.
                button.transition = Selectable.Transition.None;

                TeachingAnswerHover hover = card.gameObject.AddComponent<TeachingAnswerHover>();
                hover.panel = ctrl;
                hover.slot = i;

                TextMeshProUGUI label = NewText("Texte", card, "", 24f, ColText,
                                                TextAlignmentOptions.Center, true);
                label.enableWordWrapping = true;
                Stretch(label.rectTransform);

                answerButtons[i] = button;
                answerBackgrounds[i] = bg;
                answerTexts[i] = label;
            }

            // =====================================================
            //  3. Le resultat
            // =====================================================
            RectTransform result = NewRect("Resultat", panel);
            AnchorCenter(result, new Vector2(0f, -20f), new Vector2(1040f, 400f));

            TextMeshProUGUI resultTitle = NewText("Verdict", result, "", 46f, ColText,
                                                  TextAlignmentOptions.Top, true);
            resultTitle.fontStyle = FontStyles.Bold;
            AnchorTopCenter(resultTitle.rectTransform, new Vector2(0f, -40f), new Vector2(900f, 70f));

            TextMeshProUGUI resultBody = NewText("Explication", result, "", 24f, ColDim,
                                                 TextAlignmentOptions.Top, true);
            resultBody.enableWordWrapping = true;
            resultBody.lineSpacing = 14f;
            AnchorTopCenter(resultBody.rectTransform, new Vector2(0f, -128f), new Vector2(900f, 180f));

            // --- cablage ---
            ctrl.panelRoot = panel.gameObject;
            ctrl.group = group;
            ctrl.headerText = header;
            ctrl.taglineText = tagline;

            ctrl.teachingRoot = teaching.gameObject;
            ctrl.sourceText = source;
            ctrl.titleText = title;
            ctrl.bodyText = body;
            ctrl.counterText = counter;
            ctrl.nextButton = nextButton;
            ctrl.nextText = nextLabel;

            ctrl.questionRoot = question.gameObject;
            ctrl.questionTitleText = questionTitle;
            ctrl.situationText = situation;
            ctrl.answerButtons = answerButtons;
            ctrl.answerBackgrounds = answerBackgrounds;
            ctrl.answerTexts = answerTexts;

            ctrl.resultRoot = result.gameObject;
            ctrl.resultTitleText = resultTitle;
            ctrl.resultBodyText = resultBody;

            EnsureTeachingManager();
            EnsureIconOverrides();

            // Les cartes de reponse sont des plaques du theme : la couleur d'etat les
            // TEINTE. Blanc = la plaque telle quelle ; le survol l'eclaircit.
            ctrl.idleColor = Color.white;
            ctrl.hoverColor = new Color(1.25f, 1.35f, 1.5f, 1f);

            // L'ecran prend la place : c'est le moment de lecture du jeu.
            Enlarge(panel, 1.25f);

            question.gameObject.SetActive(false);
            result.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Marque une Image comme portant tel pictogramme, SANS y ecrire le sprite.
        ///
        /// Le sprite est resolu au lancement par IconBinder, qui passe par IconLibrary
        /// et donc par tes propres dessins s'il y en a. Ecrire le sprite ici l'aurait
        /// fige dans la scene : deposer de vrais pictogrammes aurait exige de tout
        /// reconstruire pour les voir.
        /// </summary>
        private static void Bind(Image image, MNLTHII.UI.IconKind kind)
        {
            if (image == null) return;

            MNLTHII.UI.IconBinder binder = image.gameObject.GetComponent<MNLTHII.UI.IconBinder>();
            if (binder == null) binder = image.gameObject.AddComponent<MNLTHII.UI.IconBinder>();

            binder.kind = kind;
            binder.Apply();
        }

        /// <summary>
        /// Pose la boite a pictogrammes s'il manque. C'est la que tu deposeras tes
        /// fichiers : dix champs, une seule fois, et toute l'interface suit.
        /// </summary>
        /// <summary>
        /// LE MENU D'UNE CASE : deux boutons empiles, au-dessus du point clique.
        ///   - voir de pres (cyan)
        ///   - utiliser l'energie (or), avec son prix a gauche
        /// Les clics sont cables par HexActionMenu au lancement, jamais ici.
        /// </summary>
        private static void BuildHexMenu(RectTransform root, Canvas canvas, JObject labels)
        {
            JObject hm = labels["hexMenu"] as JObject;

            // Trois boutons qu'on touche au doigt : chacun fait 76 de haut (jamais
            // moins de 44 px a l'ecran, meme sur un petit telephone).
            const float W = 440f;
            const float BtnW = 400f;
            const float BtnH = 76f;

            RectTransform panel = NewRect("MenuCase", root);
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(0f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(W, 3f * BtnH + 72f);
            panel.anchoredPosition = new Vector2(400f, 400f);

            Image bg = panel.gameObject.AddComponent<Image>();
            bg.color = ColPanel;
            bg.raycastTarget = true;

            UnityEngine.UI.Outline frame = panel.gameObject.AddComponent<UnityEngine.UI.Outline>();
            frame.effectColor = ColLine;
            frame.effectDistance = new Vector2(1f, 1f);

            // --- voir de pres ---
            RectTransform view = NewRect("VoirDePres", panel);
            AnchorTopCenter(view, new Vector2(0f, -18f), new Vector2(BtnW, BtnH));

            // Plaque sombre (voir ApplyElectronicTheme) : la seule action pleine du
            // menu est "utiliser l'Energie", en bas.
            Image viewBg = view.gameObject.AddComponent<Image>();
            viewBg.color = new Color(1f, 1f, 1f, 0.06f);

            UnityEngine.UI.Outline viewEdge = view.gameObject.AddComponent<UnityEngine.UI.Outline>();
            viewEdge.effectColor = ColLine;
            viewEdge.effectDistance = new Vector2(1f, 1f);

            Button viewButton = view.gameObject.AddComponent<Button>();
            viewButton.targetGraphic = viewBg;

            TextMeshProUGUI viewText = NewText("Libelle", view, S(hm, "view"), 22f, ColCyan,
                                               TextAlignmentOptions.Center, true);
            viewText.fontStyle = FontStyles.Bold;
            Stretch(viewText.rectTransform);

            // --- details ---
            RectTransform details = NewRect("Details", panel);
            AnchorTopCenter(details, new Vector2(0f, -18f - (BtnH + 18f)), new Vector2(BtnW, BtnH));

            Image detailsBg = details.gameObject.AddComponent<Image>();
            detailsBg.color = new Color(1f, 1f, 1f, 0.06f);

            UnityEngine.UI.Outline detailsEdge = details.gameObject.AddComponent<UnityEngine.UI.Outline>();
            detailsEdge.effectColor = ColLine;
            detailsEdge.effectDistance = new Vector2(1f, 1f);

            Button detailsButton = details.gameObject.AddComponent<Button>();
            detailsButton.targetGraphic = detailsBg;

            TextMeshProUGUI detailsText = NewText("Libelle", details, S(hm, "details"), 22f, ColText,
                                                  TextAlignmentOptions.Center, true);
            detailsText.fontStyle = FontStyles.Bold;
            Stretch(detailsText.rectTransform);

            // --- utiliser l'energie ---
            RectTransform use = NewRect("UtiliserEnergie", panel);
            AnchorTopCenter(use, new Vector2(0f, -18f - 2f * (BtnH + 18f)), new Vector2(BtnW, BtnH));

            Image useBg = use.gameObject.AddComponent<Image>();
            useBg.color = new Color(ColGold.r, ColGold.g, ColGold.b, 0.14f);

            UnityEngine.UI.Outline useEdge = use.gameObject.AddComponent<UnityEngine.UI.Outline>();
            useEdge.effectColor = new Color(ColGold.r, ColGold.g, ColGold.b, 0.5f);
            useEdge.effectDistance = new Vector2(1f, 1f);

            Button useButton = use.gameObject.AddComponent<Button>();
            useButton.targetGraphic = useBg;

            // Le libelle a droite (hebreu), le prix a gauche (chiffres, non RTL).
            TextMeshProUGUI useText = NewText("Libelle", use, S(hm, "use"), 22f, ColText,
                                              TextAlignmentOptions.MidlineRight, true);
            useText.fontStyle = FontStyles.Bold;
            useText.rectTransform.anchorMin = new Vector2(0f, 0f);
            useText.rectTransform.anchorMax = new Vector2(1f, 1f);
            useText.rectTransform.offsetMin = new Vector2(90f, 0f);
            useText.rectTransform.offsetMax = new Vector2(-18f, 0f);

            Image energyIcon = NewImage("Icone", use, ColGold);
            energyIcon.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            energyIcon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            energyIcon.rectTransform.pivot = new Vector2(0f, 0.5f);
            energyIcon.rectTransform.anchoredPosition = new Vector2(14f, 0f);
            energyIcon.rectTransform.sizeDelta = new Vector2(24f, 24f);
            energyIcon.preserveAspect = true;
            Bind(energyIcon, MNLTHII.UI.IconKind.Energy);

            TextMeshProUGUI cost = NewText("Cout", use, "", 24f, ColGold,
                                           TextAlignmentOptions.MidlineLeft, false);
            cost.fontStyle = FontStyles.Bold;
            cost.rectTransform.anchorMin = new Vector2(0f, 0f);
            cost.rectTransform.anchorMax = new Vector2(0f, 1f);
            cost.rectTransform.pivot = new Vector2(0f, 0.5f);
            cost.rectTransform.anchoredPosition = new Vector2(44f, 0f);
            cost.rectTransform.sizeDelta = new Vector2(70f, 0f);

            HexActionMenu ctrl = canvas.gameObject.GetComponent<HexActionMenu>();
            if (ctrl == null) ctrl = canvas.gameObject.AddComponent<HexActionMenu>();

            ctrl.panel = panel;
            // Le menu reste leger : pas de voile, et il s'ouvre au-dessus du point
            // clique. Seuls les details sont centres sur fond sombre.
            ctrl.dim = null;
            ctrl.centered = false;
            ctrl.viewButton = viewButton;
            ctrl.viewText = viewText;
            ctrl.useButton = useButton;
            ctrl.useBackground = useBg;
            ctrl.useText = useText;
            ctrl.costText = cost;
            ctrl.viewLabel = S(hm, "view");
            ctrl.detailsButton = detailsButton;
            ctrl.detailsText = detailsText;
            ctrl.detailsLabel = S(hm, "details");
            ctrl.useLabel = S(hm, "use");
            // Le fond du bouton est un sprite du theme (voir ApplyElectronicTheme) :
            // la couleur d'etat le TEINTE, elle ne le remplace plus. Blanc = la plaque
            // telle quelle ; eteint = la meme plaque, grise et voilee.
            ctrl.useIdleColor = Color.white;
            ctrl.useDisabledColor = new Color(0.62f, 0.66f, 0.76f, 0.45f);

            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Pose la boite a photos s'il n'y en a pas, sur l'objet des gestionnaires.
        /// Une boite deja presente n'est jamais recreee : elle contient tes images.
        /// </summary>
        private static void EnsureElementPhotos()
        {
            MNLTHII.UI.ElementPhotos existing =
                Object.FindFirstObjectByType<MNLTHII.UI.ElementPhotos>(FindObjectsInactive.Include);
            if (existing != null) return;

            LocalGameEngine engine = Object.FindFirstObjectByType<LocalGameEngine>(FindObjectsInactive.Include);

            GameObject host = (engine != null) ? engine.gameObject : new GameObject("Photos");
            host.AddComponent<MNLTHII.UI.ElementPhotos>();

            EditorUtility.SetDirty(host);
            Debug.LogFormat("[HudBuilder] Boite a photos posee sur \"{0}\" : glisse tes images dans ElementPhotos.", host.name);
        }

        /// <summary>
        /// LA VUE IMMERSIVE : le bandeau "retour a la carte" et le composant qui pilote
        /// la camera.
        ///
        /// Le bandeau est un enfant DIRECT du canevas, pas de la racine du HUD : pendant
        /// la vue, la racine est masquee en entier (CanvasGroup a 0), et le bouton
        /// retour doit rester visible. On efface donc l'ancien bandeau a la main, puisque
        /// la reconstruction du HUD n'efface que la racine.
        /// </summary>
        private static void BuildImmersive(RectTransform root, Canvas canvas, JObject labels)
        {
            JObject im = labels["immersive"] as JObject;

            const string OverlayName = "VueImmersive";
            Transform old = canvas.transform.Find(OverlayName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // Le HUD entier s'efface pendant les plans : c'est l'image qui compte.
            CanvasGroup hudGroup = root.gameObject.GetComponent<CanvasGroup>();
            if (hudGroup == null) hudGroup = root.gameObject.AddComponent<CanvasGroup>();

            RectTransform bar = NewRect(OverlayName, canvas.transform);
            AnchorBottomCenter(bar, new Vector2(0f, 40f), new Vector2(820f, 92f));

            Image bg = bar.gameObject.AddComponent<Image>();
            bg.color = ColPanel;
            bg.raycastTarget = true;

            UnityEngine.UI.Outline frame = bar.gameObject.AddComponent<UnityEngine.UI.Outline>();
            frame.effectColor = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.35f);
            frame.effectDistance = new Vector2(1f, 1f);

            // L'aide, a droite (l'hebreu se lit de droite a gauche).
            TextMeshProUGUI hint = NewText("Aide", bar, S(im, "hint"), 18f, ColDim,
                                           TextAlignmentOptions.MidlineRight, true);
            hint.rectTransform.anchorMin = new Vector2(1f, 0.5f);
            hint.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            hint.rectTransform.pivot = new Vector2(1f, 0.5f);
            hint.rectTransform.anchoredPosition = new Vector2(-28f, 0f);
            hint.rectTransform.sizeDelta = new Vector2(480f, 60f);
            hint.enableWordWrapping = true;

            // Le bouton retour, a gauche.
            RectTransform exit = NewRect("Retour", bar);
            exit.anchorMin = new Vector2(0f, 0.5f);
            exit.anchorMax = new Vector2(0f, 0.5f);
            exit.pivot = new Vector2(0f, 0.5f);
            exit.anchoredPosition = new Vector2(20f, 0f);
            exit.sizeDelta = new Vector2(270f, 60f);

            Image exitBg = exit.gameObject.AddComponent<Image>();
            exitBg.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.14f);

            UnityEngine.UI.Outline exitEdge = exit.gameObject.AddComponent<UnityEngine.UI.Outline>();
            exitEdge.effectColor = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.6f);
            exitEdge.effectDistance = new Vector2(1f, 1f);

            Button exitButton = exit.gameObject.AddComponent<Button>();
            exitButton.targetGraphic = exitBg;

            TextMeshProUGUI exitText = NewText("Libelle", exit, S(im, "exit"), 22f, ColCyan,
                                               TextAlignmentOptions.Center, true);
            exitText.fontStyle = FontStyles.Bold;
            Stretch(exitText.rectTransform);

            ImmersiveCamera ctrl = canvas.gameObject.GetComponent<ImmersiveCamera>();
            if (ctrl == null) ctrl = canvas.gameObject.AddComponent<ImmersiveCamera>();

            ctrl.overlayRoot = bar.gameObject;
            ctrl.exitButton = exitButton;
            ctrl.hudGroup = hudGroup;

            bar.gameObject.SetActive(false);
        }

        private static void EnsureIconOverrides()
        {
            MNLTHII.UI.IconOverrides existing =
                Object.FindFirstObjectByType<MNLTHII.UI.IconOverrides>(FindObjectsInactive.Include);
            if (existing != null) return;

            LocalGameEngine engine = Object.FindFirstObjectByType<LocalGameEngine>(FindObjectsInactive.Include);

            GameObject host = (engine != null) ? engine.gameObject : new GameObject("Pictogrammes");
            host.AddComponent<MNLTHII.UI.IconOverrides>();

            EditorUtility.SetDirty(host);
            Debug.LogFormat("[HudBuilder] Boite a pictogrammes posee sur \"{0}\" : depose tes fichiers dedans.", host.name);
        }

        /// <summary>
        /// Pose le lecteur d'enseignements s'il manque, sur l'objet des gestionnaires.
        /// Sans lui l'ecran serait construit et resterait vide.
        /// </summary>
        private static void EnsureTeachingManager()
        {
            TeachingManager existing = Object.FindFirstObjectByType<TeachingManager>(FindObjectsInactive.Include);
            if (existing != null) return;

            LocalGameEngine engine = Object.FindFirstObjectByType<LocalGameEngine>(FindObjectsInactive.Include);

            GameObject host = (engine != null) ? engine.gameObject : new GameObject("Enseignements");
            host.AddComponent<TeachingManager>();

            EditorUtility.SetDirty(host);
            Debug.LogFormat("[HudBuilder] Lecteur d'enseignements installe sur \"{0}\".", host.name);
        }

        /// <summary>
        /// L'ouverture d'un tour. Un numero, le revenu du tour, et c'est tout.
        ///
        /// Le panneau porte un CanvasGroup parce que le fondu doit passer par UNE
        /// valeur alpha pour l'ensemble. Faire varier la couleur de chaque texte
        /// forcerait TextMeshPro a reconstruire son maillage a chaque frame du fondu.
        /// </summary>
        private static void BuildTurnAnnounce(RectTransform root, Canvas canvas, JObject labels)
        {
            JObject ta = labels["turnAnnounce"] as JObject;

            RectTransform panel = NewRect("OuvertureDuTour", root);
            Stretch(panel);

            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            // Le voile absorbe les clics : pendant l'ouverture, rien ne doit atteindre
            // le plateau, meme un clic parti trop tot.
            group.blocksRaycasts = true;

            // LE RIDEAU : le fond du theme (bleu nuit, lueur rouge a gauche, cyan a
            // droite), le meme que celui du menu. Opaque a l'ouverture de la partie,
            // il cache le plateau qui se construit ; aux tours suivants il devient un
            // voile. Le joueur passe du menu au jeu sans changer de decor.
            Image curtain = panel.gameObject.AddComponent<Image>();
            Sprite backdrop = Art("00_fond_1280x720.png", false);
            if (backdrop == null) backdrop = UiSprite("ui_backdrop.png", false);
            if (backdrop != null) { curtain.sprite = backdrop; curtain.type = Image.Type.Simple; curtain.color = Color.white; }
            else curtain.color = new Color32(0x0A, 0x14, 0x28, 0xFF);
            curtain.raycastTarget = true;

            // Les textes, dans leur propre groupe : ils apparaissent SUR le rideau.
            RectTransform content = NewRect("Contenu", panel);
            Stretch(content);
            CanvasGroup contentGroup = content.gameObject.AddComponent<CanvasGroup>();
            contentGroup.alpha = 0f;
            contentGroup.interactable = false;
            contentGroup.blocksRaycasts = false;

            // Le numero du tour dans un grand hexagone de Dan, comme sur le bandeau.
            Sprite badge = Art("04_hexagone_normal.png", false);
            if (badge == null) badge = UiSprite("ui_hex.png", false);
            RectTransform hex = NewRect("Hexagone", content);
            AnchorCenter(hex, new Vector2(0f, 20f), new Vector2(360f, 360f));
            Image hexImage = hex.gameObject.AddComponent<Image>();
            hexImage.raycastTarget = false;
            hexImage.preserveAspect = true;
            if (badge != null) hexImage.sprite = badge; else hexImage.color = ColPanel;

            TextMeshProUGUI turnLabel = NewText("Libelle", hex, S(ta, "turnLabel"), 26f, ColDim,
                                                TextAlignmentOptions.Center, true);
            AnchorCenter(turnLabel.rectTransform, new Vector2(0f, 78f), new Vector2(300f, 44f));

            TextMeshProUGUI turnNumber = NewText("Numero", hex, "", 110f, ColCyan,
                                                 TextAlignmentOptions.Center, false);
            turnNumber.fontStyle = FontStyles.Bold;
            AnchorCenter(turnNumber.rectTransform, new Vector2(0f, -18f), new Vector2(300f, 170f));

            Image ruleTop = NewImage("Filet_Haut", content, new Color(ColSteel.r, ColSteel.g, ColSteel.b, 0.35f));
            AnchorCenter(ruleTop.rectTransform, new Vector2(0f, 230f), new Vector2(620f, 2f));

            Image ruleBottom = NewImage("Filet_Bas", content, new Color(ColSteel.r, ColSteel.g, ColSteel.b, 0.35f));
            AnchorCenter(ruleBottom.rectTransform, new Vector2(0f, -190f), new Vector2(620f, 2f));

            // En hebreu la ligne se lit de droite a gauche : le libelle ouvre a droite,
            // le nombre vient ensuite, donc a sa gauche.
            TextMeshProUGUI incomeLabel = NewText("Revenu_Libelle", content, S(ta, "incomeLabel"),
                                                  24f, ColDim, TextAlignmentOptions.Top, true);
            AnchorCenter(incomeLabel.rectTransform, new Vector2(110f, -236f), new Vector2(300f, 48f));

            TextMeshProUGUI incomeValue = NewText("Revenu_Valeur", content, "", 34f, ColCyan,
                                                  TextAlignmentOptions.Top, false);
            incomeValue.fontStyle = FontStyles.Bold;
            AnchorCenter(incomeValue.rectTransform, new Vector2(-110f, -240f), new Vector2(220f, 56f));

            TurnAnnouncePanel ctrl = canvas.gameObject.GetComponent<TurnAnnouncePanel>();
            if (ctrl == null) ctrl = canvas.gameObject.AddComponent<TurnAnnouncePanel>();

            ctrl.panelRoot = panel.gameObject;
            ctrl.group = group;
            ctrl.curtain = curtain;
            ctrl.content = contentGroup;
            ctrl.turnLabelText = turnLabel;
            ctrl.turnNumberText = turnNumber;
            ctrl.incomeLabelText = incomeLabel;
            ctrl.incomeValueText = incomeValue;
            ctrl.ruleTop = ruleTop;
            ctrl.ruleBottom = ruleBottom;

            ctrl.turnLabel = S(ta, "turnLabel");
            ctrl.incomeLabel = S(ta, "incomeLabel");
            ctrl.numberFormat = S(ta, "numberFormat", "{0}");
            ctrl.incomeFormat = S(ta, "incomeFormat", "+{0}");

            ctrl.fadeInDuration = 0.35f;
            ctrl.holdDuration = 1.7f;
            ctrl.fadeOutDuration = 0.45f;

            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Installe le cadreur de camera s'il manque.
        ///
        /// Il ne vit pas sur le Canvas : ce n'est pas de l'interface, c'est de la mise
        /// en scene. Sa place est sur le PIVOT de la camera - le parent - et surtout
        /// pas sur la camera elle-meme, qui porte un Animator dans cette scene : tout
        /// ce qu'un script ecrirait sur son transform serait ecrase a la frame
        /// suivante par le systeme d'animation.
        ///
        /// Si la camera n'a pas de parent, on pose quand meme le composant sur elle et
        /// on previent : le cadrage fonctionnera, sauf si un Animator le combat.
        /// </summary>
        /// <summary>
        /// Pose le gestionnaire de cordons s'il manque.
        ///
        /// Il n'a aucune reference a cabler et ne se voit pas dans la hierarchie : sans
        /// cette ligne, on livrerait un projet ou l'eclair entre un ennemi et son Shofar
        /// n'existe que si quelqu'un a pense a ajouter le composant a la main.
        /// </summary>
        private static void EnsurePortalTether()
        {
            PortalTether existing = Object.FindFirstObjectByType<PortalTether>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Debug.Log("[HudBuilder] Cordons ennemi-Shofar deja presents.");
                return;
            }

            // Sur l'objet des gestionnaires. Attention : PortalManager, BuildingManager
            // et compagnie n'existent PAS dans la scene enregistree - LocalGameEngine
            // les ajoute lui-meme au demarrage. Chercher PortalManager ici ne trouverait
            // donc rien et creerait un objet orphelin ; c'est LocalGameEngine qu'il faut
            // viser, puisque c'est lui qui porte tout le reste.
            LocalGameEngine engine = Object.FindFirstObjectByType<LocalGameEngine>(FindObjectsInactive.Include);

            GameObject host = (engine != null)
                ? engine.gameObject
                : new GameObject("Cordons Shofar");

            host.AddComponent<PortalTether>();

            EditorUtility.SetDirty(host);
            Debug.LogFormat("[HudBuilder] Cordons ennemi-Shofar installes sur \"{0}\".", host.name);
        }

        private static void EnsureCameraDirector()
        {
            CameraDirector existing = Object.FindFirstObjectByType<CameraDirector>();
            if (existing != null)
            {
                Debug.Log("[HudBuilder] Cadreur de camera deja present.");
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[HudBuilder] Aucune camera taguee MainCamera : le cadrage n'a pas ete installe.");
                return;
            }

            Transform pivot = cam.transform.parent;

            if (pivot == null)
            {
                pivot = cam.transform;
                Debug.LogWarning("[HudBuilder] La camera n'a pas de parent. Le cadreur est pose sur elle, "
                               + "mais si elle porte un Animator il ecrasera les mouvements : "
                               + "mets-la sous un objet vide et relance.");
            }

            CameraDirector director = pivot.gameObject.AddComponent<CameraDirector>();
            director.pivot = pivot;

            // L'apercu de menace lit la cadence du cadreur pour se synchroniser : on
            // aligne les deux ici, d'un seul endroit.
            ThreatPreview preview = Object.FindFirstObjectByType<ThreatPreview>(FindObjectsInactive.Include);
            if (preview != null)
            {
                preview.tourHoldPerThreat = 2.2f;
                EditorUtility.SetDirty(preview);
            }

            EditorUtility.SetDirty(pivot.gameObject);
            Debug.LogFormat("[HudBuilder] Cadreur de camera installe sur \"{0}\".", pivot.name);
        }

        /// <summary>
        /// L'ecran de fin : victoire ou defaite.
        ///
        /// Une seule mise en page pour les deux. Seuls le titre, la couleur et la
        /// phrase changent - deux ecrans distincts auraient coute deux fois le travail
        /// pour rendre le jeu moins lisible.
        ///
        /// Trois chiffres sous le titre. Une fin sans bilan n'apprend rien : c'est la
        /// difference entre "j'ai perdu" et "j'ai perdu au tour 14 avec deux Shofars
        /// encore ouverts", et c'est la seconde phrase qui donne envie de rejouer.
        /// </summary>
        private static void BuildGameOver(RectTransform root, Canvas canvas, JObject labels)
        {
            JObject go = labels["gameOver"] as JObject;

            const float PanelW = 820f;
            const float PanelH = 560f;

            RectTransform screen = NewRect("FinDePartie", root);
            Stretch(screen);

            CanvasGroup group = screen.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            Image dim = screen.gameObject.AddComponent<Image>();
            dim.color = new Color32(0x03, 0x05, 0x0B, 0xEB);
            dim.raycastTarget = true;

            // --- la carte centrale ---
            RectTransform panel = NewRect("Carte", screen);
            AnchorCenter(panel, Vector2.zero, new Vector2(PanelW, PanelH));

            Image bg = panel.gameObject.AddComponent<Image>();
            bg.color = ColPanel;
            bg.raycastTarget = false;

            UnityEngine.UI.Outline frame = panel.gameObject.AddComponent<UnityEngine.UI.Outline>();
            frame.effectColor = ColLine;
            frame.effectDistance = new Vector2(1f, 1f);

            Image accent = NewImage("Liseret", panel, ColCyan);
            AnchorTopRight(accent.rectTransform, Vector2.zero, new Vector2(PanelW, 3f));

            TextMeshProUGUI title = NewText("Titre", panel, "", 64f, ColCyan,
                                            TextAlignmentOptions.Top, true);
            title.fontStyle = FontStyles.Bold;
            AnchorTopCenter(title.rectTransform, new Vector2(0f, -56f), new Vector2(PanelW - 60f, 106f));

            TextMeshProUGUI subtitle = NewText("SousTitre", panel, "", 21f, ColDim,
                                               TextAlignmentOptions.Top, true);
            AnchorTopCenter(subtitle.rectTransform, new Vector2(0f, -176f), new Vector2(PanelW - 100f, 44f));

            Image rule = NewImage("Filet", panel, ColLine);
            AnchorTopCenter(rule.rectTransform, new Vector2(0f, -240f), new Vector2(PanelW - 120f, 1f));

            // --- les trois chiffres, lus de droite a gauche ---
            TextMeshProUGUI[] statLabels = new TextMeshProUGUI[3];
            TextMeshProUGUI[] statValues = new TextMeshProUGUI[3];

            const float ColumnW = 240f;
            float firstX = ColumnW;      // colonne 0 a droite, comme la lecture

            for (int i = 0; i < 3; i++)
            {
                float x = firstX - i * ColumnW;

                RectTransform column = NewRect("Bilan" + i, panel);
                AnchorTopCenter(column, new Vector2(x, -278f), new Vector2(ColumnW, 140f));

                TextMeshProUGUI value = NewText("Valeur", column, "", 46f, ColText,
                                                TextAlignmentOptions.Top, false);
                value.fontStyle = FontStyles.Bold;
                AnchorTopCenter(value.rectTransform, Vector2.zero, new Vector2(ColumnW, 78f));

                TextMeshProUGUI label = NewText("Libelle", column, "", 16f, ColDim,
                                                TextAlignmentOptions.Top, true);
                AnchorTopCenter(label.rectTransform, new Vector2(0f, -82f), new Vector2(ColumnW, 34f));

                statValues[i] = value;
                statLabels[i] = label;
            }

            // --- reprise ---
            RectTransform replay = NewRect("Rejouer", panel);
            AnchorBottomCenter(replay, new Vector2(0f, 44f), new Vector2(340f, 78f));

            Image replayBg = replay.gameObject.AddComponent<Image>();
            replayBg.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.12f);

            UnityEngine.UI.Outline replayEdge = replay.gameObject.AddComponent<UnityEngine.UI.Outline>();
            replayEdge.effectColor = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.55f);
            replayEdge.effectDistance = new Vector2(1.5f, 1.5f);

            Button replayButton = replay.gameObject.AddComponent<Button>();
            replayButton.targetGraphic = replayBg;

            TextMeshProUGUI replayText = NewText("Texte", replay, "", 22f,
                                                 new Color32(0xBF, 0xF6, 0xEF, 0xFF),
                                                 TextAlignmentOptions.Center, true);
            Stretch(replayText.rectTransform);

            // --- cablage ---
            GameOverPanel ctrl = canvas.gameObject.GetComponent<GameOverPanel>();
            if (ctrl == null) ctrl = canvas.gameObject.AddComponent<GameOverPanel>();

            ctrl.panelRoot = screen.gameObject;
            ctrl.group = group;
            ctrl.dimmer = dim;
            ctrl.accentBar = accent;
            ctrl.titleText = title;
            ctrl.subtitleText = subtitle;
            ctrl.statLabels = statLabels;
            ctrl.statValues = statValues;
            ctrl.replayButton = replayButton;
            ctrl.replayText = replayText;

            ctrl.victoryTitle = S(go, "victoryTitle");
            ctrl.victorySubtitle = S(go, "victorySubtitle");
            ctrl.defeatTitle = S(go, "defeatTitle");
            ctrl.defeatSubtitle = S(go, "defeatSubtitle");
            ctrl.replayLabel = S(go, "replayLabel");

            ctrl.statTurnsLabel = S(go, "statTurns");
            ctrl.statPortalsLabel = S(go, "statPortals");
            ctrl.statKillsLabel = S(go, "statKills");
            ctrl.portalsFormat = S(go, "portalsFormat", "{0}/{1}");

            Enlarge(screen, 1.3f);

            screen.gameObject.SetActive(false);
        }

        /// <summary>Lit un tableau de chaines du JSON, complete a la taille voulue.</summary>
        private static string[] ReadArray(JObject o, string key, int size)
        {
            string[] result = new string[size];
            JArray array = (o != null) ? o[key] as JArray : null;

            for (int i = 0; i < size; i++)
                result[i] = (array != null && i < array.Count) ? array[i].ToString() : "";

            return result;
        }

        private static void ApplyFormats(HudController hud, JObject labels)
        {
            hud.energyDeltaFormat = S(labels, "energyDeltaFormat", "+{0}");
            hud.portalCountFormat = S(labels, "portalCountFormat", "{0}/{1}");
            hud.tankCountFormat = S(labels, "tankCountFormat", "{0}/{1}");
            hud.turnFormat = "{0}";
        }

        // =================================================================
        //  PRIMITIVES
        // =================================================================
        // =================================================================
        //  LE THEME ELECTRONIQUE
        // =================================================================
        //
        // Les panneaux etaient des rectangles plats a liseres (Image + Outline). Le
        // jeu est un jeu de robots : les plaques prennent un chanfrein, un lisere
        // lumineux, une trame hexagonale en fond, comme le logo. Les sprites vivent
        // dans Assets/Resources/UI (ui_plate_*, ui_button_*, ui_panel_dark,
        // ui_hexgrid) ; ils sont decoupes en 9 (bords de 30 px) pour s'etirer sans
        // deformer les coins.
        //
        // Le theme est applique EN DERNIER, sur ce que les Build ont pose, d'apres
        // la couleur de chaque fond :
        //   - un fond ColPanel (les grands panneaux)      -> plaque cyan + trame
        //   - un fond de bouton teinte cyan / or / rouge  -> bouton de la couleur
        // Les cartes a survol (choix du Tank, reponses) gardent leur fond plat : leur
        // couleur change a l'execution, un sprite blanc teinte a 6 % disparaitrait.
        private const string UiSpriteDir = "Assets/Resources/UI/";
        private const float UiSpriteBorder = 40f;

        private static Sprite UiSprite(string file, bool sliced)
        {
            string path = UiSpriteDir + file;
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarningFormat("[HudBuilder] Sprite du theme introuvable : {0}. Le HUD garde ses fonds plats.", path);
                return null;
            }

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }

            Vector4 border = sliced ? new Vector4(UiSpriteBorder, UiSpriteBorder, UiSpriteBorder, UiSpriteBorder) : Vector4.zero;
            if (importer.spriteBorder != border) { importer.spriteBorder = border; dirty = true; }

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteMeshType != SpriteMeshType.FullRect || settings.wrapMode != TextureWrapMode.Clamp)
            {
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.wrapMode = TextureWrapMode.Clamp;
                importer.SetTextureSettings(settings);
                dirty = true;
            }

            if (dirty) importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Une plaque posee DERRIERE un bloc de textes, un peu plus large que lui.</summary>
        private static void PlateBehind(RectTransform block, string file, float alpha, Vector2 padding)
        {
            Sprite sprite = UiSprite(file, true);
            if (sprite == null) return;

            RectTransform plate = NewRect("Plaque", block);
            plate.anchorMin = Vector2.zero;
            plate.anchorMax = Vector2.one;
            plate.offsetMin = new Vector2(-padding.x, -padding.y);
            plate.offsetMax = new Vector2(padding.x, padding.y);
            plate.SetAsFirstSibling();

            Image image = plate.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = new Color(1f, 1f, 1f, alpha);
            image.raycastTarget = false;
        }

        private static bool SameColor(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f
                && Mathf.Abs(a.b - b.b) < 0.02f && Mathf.Abs(a.a - b.a) < 0.03f;
        }

        private static bool SameHue(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f && Mathf.Abs(a.b - b.b) < 0.02f;
        }

        private static void Skin(Image image, Sprite sprite, float alpha)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = new Color(1f, 1f, 1f, alpha);

            UnityEngine.UI.Outline outline = image.GetComponent<UnityEngine.UI.Outline>();
            if (outline != null) Object.DestroyImmediate(outline);
        }

        /// <summary>
        /// Tous les sprites du theme importes comme il faut, y compris ceux que seul
        /// le code d'execution charge (menu principal, barre de phase, intro) : sans
        /// ce passage, Resources.Load&lt;Sprite&gt; rendrait null sur une texture
        /// encore importee en "Default".
        /// </summary>
        [MenuItem("Milchemet/Importer les sprites du theme", false, 12)]
        private static void EnsureAllUiSpritesMenu()
        {
            EnsureAllUiSprites();
            Debug.Log("[HudBuilder] Sprites du theme importes (Resources/UI).");
        }

        internal static void EnsureAllUiSpritesPublic()
        {
            EnsureAllUiSprites();
        }

        private static void EnsureAllUiSprites()
        {
            string[] sliced = { "ui_plate_cyan.png", "ui_plate_gold.png", "ui_plate_red.png", "ui_plate_steel.png",
                                "ui_button_cyan.png", "ui_button_gold.png", "ui_button_red.png",
                                "ui_button_dark.png", "ui_button_dark_red.png", "ui_panel_dark.png" };
            string[] plain = { "ui_hexgrid.png", "ui_scanlines.png", "ui_vignette.png",
                               "ui_glowline.png", "ui_bracket.png",
                               "ui_hex.png", "ui_hex_cyan.png", "ui_hex_red.png", "ui_hex_dim_red.png",
                               "ui_hex_dim_blue.png", "ui_hex_badge.png", "ui_title_plate.png", "ui_backdrop.png" };
            for (int i = 0; i < sliced.Length; i++) UiSprite(sliced[i], true);
            for (int i = 0; i < plain.Length; i++) UiSprite(plain[i], false);
        }

        private static void ApplyElectronicTheme(RectTransform root)
        {
            EnsureAllUiSprites();

            Sprite panelDark = UiSprite("ui_panel_dark.png", true);
            Sprite plateSteel = UiSprite("ui_plate_steel.png", true);
            Sprite buttonCyan = UiSprite("ui_button_cyan.png", true);
            Sprite buttonDark = UiSprite("ui_button_dark.png", true);
            Sprite buttonRed = UiSprite("ui_button_dark_red.png", true);
            Sprite backdrop = Art("00_fond_1280x720.png", false);
            if (backdrop == null) backdrop = UiSprite("ui_backdrop.png", false);
            Sprite hexgrid = UiSprite("ui_hexgrid.png", false);
            if (panelDark == null || buttonCyan == null || buttonDark == null) return;

            Color pageBg = new Color32(0x0B, 0x11, 0x20, 0xF2);
            Color oldGold = new Color32(0xFF, 0xC6, 0x5C, 0xFF);

            Image[] images = root.GetComponentsInChildren<Image>(true);
            int plates = 0, buttons = 0, curtains = 0, frames = 0;

            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image == null || image.sprite != null) continue;   // icones, photos, pastilles, deja habille : on ne touche pas
                if (image.name == "Plaque" || image.name == "Trame") continue;

                Color c = image.color;
                RectTransform rect = image.rectTransform;
                bool stretched = rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one;

                // 1. LES VOILES PLEIN ECRAN des panneaux modaux : le fond du theme, le
                //    meme que le menu et l'ouverture du tour. Le plateau disparait
                //    presque entierement derriere, et l'ecran est "dans le jeu" au lieu
                //    d'etre un rectangle noir pose dessus.
                if (stretched && c.r < 0.1f && c.g < 0.1f && c.b < 0.15f && c.a >= 0.80f && backdrop != null)
                {
                    image.sprite = backdrop;
                    image.type = Image.Type.Simple;
                    image.preserveAspect = false;
                    image.color = new Color(1f, 1f, 1f, 0.96f);
                    curtains++;
                    continue;
                }

                // 2. LES PANNEAUX (fond bleu nuit plein) : la grande plaque acier.
                if (SameColor(c, ColPanel) || SameColor(c, pageBg))
                {
                    Skin(image, panelDark, 1f);
                    AddTrame(image.rectTransform, hexgrid);
                    plates++;
                    continue;
                }

                // 3. LES BOUTONS ET LES CARTES : un fond teinte translucide, en general
                //    avec un Outline. Le cyan et l'or etaient les deux couleurs d'action :
                //    ils deviennent le bouton plein bleu (l'action principale). Le rouge
                //    reste une plaque sombre a bord rouge. Tout le reste - les cartes de
                //    reponse, de posture, de conseil - devient une plaque sombre a bord
                //    acier, que le survol eclaircit.
                bool isButton = image.GetComponent<Button>() != null;
                bool hasOutline = image.GetComponent<UnityEngine.UI.Outline>() != null;

                if (isButton && c.a <= 0.30f)
                {
                    if (SameHue(c, ColCyan) || SameHue(c, ColGold) || SameHue(c, oldGold)) Skin(image, buttonCyan, 1f);
                    else if (SameHue(c, ColRed) && buttonRed != null) Skin(image, buttonRed, 1f);
                    else Skin(image, buttonDark, 1f);
                    buttons++;
                    continue;
                }

                // 4. LES CADRES decoratifs (un Outline, pas de bouton, fond leger) : une
                //    plaque acier. Les barres de vie et les traits n'ont pas d'Outline,
                //    ils ne sont pas concernes.
                if (!isButton && hasOutline && c.a <= 0.30f && plateSteel != null)
                {
                    Skin(image, plateSteel, 1f);
                    frames++;
                    continue;
                }
            }

            // 5. LES TEXTES LONGS s'adaptent a leur boite. Tout texte qui a le droit
            //    de passer a la ligne est un texte dont on ne connait pas la longueur
            //    (enseignements, questions, conseils, explications) : il retrecit si
            //    il le faut, jamais sous 20, au lieu de deborder de son cadre.
            TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            int fitted = 0;
            for (int i = 0; i < texts.Length; i++)
            {
                TextMeshProUGUI t = texts[i];
                if (t == null || !t.enableWordWrapping || t.enableAutoSizing) continue;
                t.enableAutoSizing = true;
                t.fontSizeMax = t.fontSize;
                t.fontSizeMin = Mathf.Max(20f, t.fontSize * 0.6f);
                t.overflowMode = TextOverflowModes.Overflow;
                fitted++;
            }

            Debug.LogFormat("[HudBuilder] Theme : {0} voiles, {1} plaques, {2} boutons, {3} cadres, {4} textes ajustables.",
                            curtains, plates, buttons, frames, fitted);
        }

        /// <summary>
        /// Agrandit le CONTENU d'un panneau modal : tout ce qui est deja pose dedans
        /// passe dans un conteneur mis a l'echelle. Les ecrans qui prennent toute la
        /// place - enseignement, question, choix de posture, fin de partie - doivent
        /// la prendre vraiment : sur un telephone, un panneau qui occupe la moitie de
        /// l'ecran est un panneau qu'on lit en plissant les yeux.
        /// </summary>
        private static void Enlarge(RectTransform panel, float scale)
        {
            if (panel == null || scale <= 1.001f) return;

            RectTransform content = NewRect("Contenu", panel);
            Stretch(content);
            content.localScale = new Vector3(scale, scale, 1f);

            // Les enfants existants (sauf le conteneur lui-meme) passent dedans, dans
            // le meme ordre : l'ordre des freres est l'ordre de dessin.
            System.Collections.Generic.List<Transform> children = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < panel.childCount; i++)
            {
                Transform child = panel.GetChild(i);
                if (child != content) children.Add(child);
            }
            for (int i = 0; i < children.Count; i++) children[i].SetParent(content, false);
        }

        /// <summary>La trame hexagonale, en fond d'une plaque, sous son contenu.</summary>
        private static void AddTrame(RectTransform plate, Sprite hexgrid)
        {
            if (hexgrid == null) return;

            RectTransform trame = NewRect("Trame", plate);
            trame.anchorMin = Vector2.zero;
            trame.anchorMax = Vector2.one;
            trame.offsetMin = new Vector2(10f, 10f);
            trame.offsetMax = new Vector2(-10f, -10f);
            trame.SetAsFirstSibling();

            Image image = trame.gameObject.AddComponent<Image>();
            image.sprite = hexgrid;
            image.type = Image.Type.Tiled;   // a sa taille native, jamais etiree
            image.color = new Color(1f, 1f, 1f, 0.16f);
            image.raycastTarget = false;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            RectTransform rect = NewRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// rightToLeft : vrai pour tout texte hebreu, faux pour les nombres.
        /// Un nombre en RTL voit ses chiffres s'inverser - 12/50 devient 50/12.
        /// </summary>
        private static TextMeshProUGUI NewText(string name, Transform parent, string content,
                                               float size, Color color,
                                               TextAlignmentOptions align, bool rightToLeft)
        {
            RectTransform rect = NewRect(name, parent);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();

            if (_font != null) text.font = _font;
            text.text = content;

            float scaled = size * TextScale;
            if (scaled < MinFontSize) scaled = MinFontSize;
            text.fontSize = scaled;
            text.color = color;
            text.alignment = align;
            text.isRightToLeftText = rightToLeft;
            text.raycastTarget = false;
            text.enableWordWrapping = false;

            DisableFontFeatures(text);

            return text;
        }

        /// <summary>
        /// Font Features sur "Nothing", pas sur "kern".
        ///
        /// TextMeshPro active le crenage par defaut. Sur du latin c'est souhaitable ;
        /// sur de l'hebreu en RTL ca ne l'est pas. Les paires de crenage des polices
        /// hebraiques sont pensees pour un flux gauche-droite, et TMP les applique
        /// apres avoir inverse l'ordre des glyphes : les decalages tombent du mauvais
        /// cote de la lettre. Resultat, des mots qui se resserrent ou se decollent au
        /// hasard, et les lettres finales qui viennent mordre la suivante.
        ///
        /// On vide donc la liste des features actives plutot que d'aller la decocher
        /// a la main sur chaque TextMeshProUGUI.
        ///
        /// Passage par SerializedObject et non par l'API publique : le nom de ce
        /// champ a change entre les versions de TMP (m_enableKerning avant, une liste
        /// m_ActiveFontFeatures ensuite). En cherchant les deux, le constructeur
        /// compile et fonctionne quelle que soit la version installee, au lieu de
        /// casser a la prochaine mise a jour du paquet.
        /// </summary>
        internal static void DisableFontFeatures(TextMeshProUGUI text)
        {
            if (text == null) return;

            SerializedObject so = new SerializedObject(text);
            bool touched = false;

            // TMP recent : liste de features OpenType actives. Vide = "Nothing".
            SerializedProperty features = so.FindProperty("m_ActiveFontFeatures");
            if (features != null && features.isArray)
            {
                features.ClearArray();
                touched = true;
            }

            // TMP ancien : un simple booleen de crenage.
            SerializedProperty kerning = so.FindProperty("m_enableKerning");
            if (kerning == null) kerning = so.FindProperty("m_EnableKerning");
            if (kerning != null && kerning.propertyType == SerializedPropertyType.Boolean)
            {
                kerning.boolValue = false;
                touched = true;
            }

            if (touched) so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void AnchorTopRight(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        private static void AnchorTopLeft(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        private static void AnchorTopCenter(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        private static void AnchorBottomCenter(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        private static void AnchorCenter(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        /// <summary>Ancrage centre horizontalement, aligne sur le bord DROIT du bloc.</summary>
        private static void AnchorCenterRight(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        /// <summary>Ancrage centre horizontalement, aligne sur le bord GAUCHE du bloc.</summary>
        private static void AnchorCenterLeft(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }
    }
}
