#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using MNLTHII.Data;
using MNLTHII.UI;

namespace MNLTHII.Tests
{
    /// <summary>
    /// LE CONTENU ET LES RESSOURCES : ce qui ne se compile pas mais casse quand meme.
    /// Libelles hebreux, enseignements, photos, shaders, scenes du build, et la regle
    /// "aucun caractere non-ASCII dans un .cs".
    /// </summary>
    public class ContentTests
    {
        // Toutes les cles lues par le code a l'execution (HudLabelsRuntime.Get, IntroTour,
        // PhaseBanner, menus, enseignements...). Une cle absente du JSON = un texte vide
        // dans le jeu, sans aucune erreur dans la console. C'est ici qu'on l'attrape.
        private static readonly string[] RuntimeKeys =
        {
            "Halacha", "Musar", "Tanakh", "Tefillah",
            "bannerBloom", "bannerBuildings", "bannerEnemies", "bannerSpending", "bannerTanks",
            "barrenBody", "barrenTitle", "baseHitPopup", "bubbleBrokenPopup", "bunker",
            "confirmCancel", "confirmUpgrade", "crackBrokenPopup", "crackPopupFormat", "crystal",
            "crystalBody", "crystalCurrent", "crystalName", "details",
            "effectBunker", "effectCrystal", "effectGas", "effectMountain", "effectTankCreate",
            "effectTankEvolve", "effectTurnPortal", "gas", "header",
            "introBaseBody", "introBaseTitle", "introBunkerBody", "introBunkerTitle",
            "introCommandBody", "introCommandTitle", "introCrystalBody", "introCrystalTitle",
            "introEnemyBody", "introEnemyTitle", "introGasBody", "introGasTitle",
            "introGroundBody", "introGroundTitle", "introOpenBody", "introOpenTitle",
            "introPortalBody", "introPortalTitle", "introRangeBody", "introRangeTitle", "introSkip",
            "introStrategyBody", "introStrategyClose", "introStrategyTitle", "introTankBody", "introTankTitle",
            "introTurnBody", "introTurnTitle",
            "mapFortress", "mapFortressBody", "mapGarden", "mapGardenBody", "mapJourney", "mapJourneyBody",
            "mapWall", "mapWallBody",
            "menuEasy", "menuEasyBody", "menuHard", "menuHardBody", "menuLoadGame", "menuMapFormat",
            "menuMedium", "menuMediumBody", "menuNewGame", "menuSubtitle", "menuTitle",
            "outOfRange", "outOfRangeWhy",
            "advisorReason9", "repairBaseEffect", "repairBaseTitle", "tankCapWhy", "tankCountFormat",
            "tanks", "titleSeekCrystal", "useRepairBase", "useTankCap",
            "paceBlitz", "paceFast", "paceHint", "paceNext", "paceNormal", "paceSkipAll",
            "pickAssault", "pickGuard", "pickHint", "pickHunt", "portalShadow", "portalShadowWhy",
            "rankBeinoni", "rankBeinoniBody", "rankTzadikRa", "rankTzadikRaBody", "rankTzadikTov", "rankTzadikTovBody",
            "repelRadius",
            "savesClose", "savesDetailFormat", "savesEmpty", "savesHeadFormat", "savesHint", "savesTitle",
            "shieldIntactBody", "shieldLabel", "surgePopup", "threatHide", "threatShow", "titleGas", "turnPortal",
            "useMaxLevel", "useOccupied", "useTooExpensive", "wearPerShot",
            "energy", "endTurn", "keySpace", "energyDeltaFormat", "portalCountFormat"
        };

        private static string ProjectRoot { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); } }

        // =================================================================
        //  LIBELLES
        // =================================================================
        [Test]
        public void HudLabels_Est_Un_JSON_Valide()
        {
            TextAsset asset = Resources.Load<TextAsset>("hud_labels");
            Assert.IsNotNull(asset, "Resources/hud_labels.json introuvable");
            JObject root = JObject.Parse(asset.text);   // leve si le JSON est casse
            Assert.IsNotNull(root);
        }

        [Test]
        public void HudLabels_Toutes_Les_Cles_Lues_Par_Le_Code_Existent()
        {
            List<string> missing = new List<string>();
            for (int i = 0; i < RuntimeKeys.Length; i++)
            {
                string value = HudLabelsRuntime.Get(RuntimeKeys[i], null);
                if (string.IsNullOrEmpty(value)) missing.Add(RuntimeKeys[i]);
            }
            Assert.IsEmpty(missing, "cles absentes ou vides dans hud_labels.json : " + string.Join(", ", missing.ToArray()));
        }

        [Test]
        public void HudLabels_Aucune_Cle_En_Double_Avec_Des_Valeurs_Differentes()
        {
            // HudLabelsRuntime prend la PREMIERE occurrence d'une cle, ou qu'elle soit :
            // deux sections qui definissent "title" differemment, et l'une des deux ment.
            TextAsset asset = Resources.Load<TextAsset>("hud_labels");
            JObject root = JObject.Parse(asset.text);

            Dictionary<string, string> first = new Dictionary<string, string>();
            List<string> conflicts = new List<string>();
            Walk(root, first, conflicts);

            Assert.IsEmpty(conflicts, "cles definies deux fois avec des valeurs differentes : " + string.Join(", ", conflicts.ToArray()));
        }

        private static void Walk(JObject obj, Dictionary<string, string> first, List<string> conflicts)
        {
            foreach (KeyValuePair<string, JToken> kv in obj)
            {
                if (kv.Key == "_comment") continue;
                JObject child = kv.Value as JObject;
                if (child != null) { Walk(child, first, conflicts); continue; }

                string value = kv.Value.ToString();
                string seen;
                if (first.TryGetValue(kv.Key, out seen))
                {
                    if (seen != value && !conflicts.Contains(kv.Key)) conflicts.Add(kv.Key);
                }
                else first[kv.Key] = value;
            }
        }

        [Test]
        public void HudLabels_Les_Formats_Ont_Leurs_Arguments()
        {
            // Un format sans {0} fait disparaitre le chiffre ; avec un {2} de trop, string.Format leve.
            Assert.IsTrue(HudLabelsRuntime.Get("crackPopupFormat").Contains("{0}") && HudLabelsRuntime.Get("crackPopupFormat").Contains("{1}"), "crackPopupFormat");
            Assert.IsTrue(HudLabelsRuntime.Get("portalCountFormat").Contains("{0}"), "portalCountFormat");
            Assert.IsTrue(HudLabelsRuntime.Get("energyDeltaFormat").Contains("{0}"), "energyDeltaFormat");
            Assert.IsTrue(HudLabelsRuntime.Get("menuMapFormat").Contains("{0}"), "menuMapFormat");
            Assert.IsTrue(HudLabelsRuntime.Get("savesHeadFormat").Contains("{0}"), "savesHeadFormat");
            Assert.DoesNotThrow(() => string.Format(HudLabelsRuntime.Get("crackPopupFormat"), 1, 4), "crackPopupFormat se formate avec 2 arguments");
            Assert.DoesNotThrow(() => string.Format(HudLabelsRuntime.Get("savesHeadFormat"), 1, 2, 3), "savesHeadFormat se formate");
            Assert.DoesNotThrow(() => string.Format(HudLabelsRuntime.Get("savesDetailFormat"), 1, 2, 3, 4), "savesDetailFormat se formate");
        }

        [Test]
        public void HudLabels_Fallback()
        {
            Assert.AreEqual("x", HudLabelsRuntime.Get("cleQuiNexistePas_123", "x"));
            Assert.AreEqual("", HudLabelsRuntime.Get("cleQuiNexistePas_123"));
            Assert.AreEqual("x", HudLabelsRuntime.Get("", "x"));
            Assert.AreEqual("x", HudLabelsRuntime.Get(null, "x"));
        }

        // =================================================================
        //  ENSEIGNEMENTS
        // =================================================================
        [Test]
        public void Teachings_Structure_Complete()
        {
            TextAsset asset = Resources.Load<TextAsset>("teachings");
            Assert.IsNotNull(asset, "Resources/teachings.json introuvable");
            JObject root = JObject.Parse(asset.text);

            JArray teachings = root["teachings"] as JArray;
            Assert.IsNotNull(teachings, "pas de tableau 'teachings'");
            Assert.GreaterOrEqual(teachings.Count, 20, "au moins vingt enseignements");

            JObject ui = root["ui"] as JObject;
            Assert.IsNotNull(ui, "pas de section 'ui'");
            foreach (string k in new[] { "header", "tagline", "next", "questionTitle" })
                Assert.IsFalse(string.IsNullOrEmpty((string)ui[k]), "ui." + k + " vide");

            int withQuestion = 0;
            for (int i = 0; i < teachings.Count; i++)
            {
                JObject t = teachings[i] as JObject;
                Assert.IsNotNull(t, "enseignement " + i + " n'est pas un objet");
                Assert.IsFalse(string.IsNullOrEmpty((string)t["title"]), "enseignement " + i + " : titre vide");
                Assert.IsFalse(string.IsNullOrEmpty((string)t["text"]), "enseignement " + i + " : texte vide");
                Assert.IsFalse(string.IsNullOrEmpty((string)t["source"]), "enseignement " + i + " : source vide");

                JObject q = t["question"] as JObject;
                if (q == null) continue;
                withQuestion++;

                JArray answers = q["answers"] as JArray;
                Assert.IsNotNull(answers, "enseignement " + i + " : question sans reponses");
                Assert.IsTrue(answers.Count >= 2 && answers.Count <= 4, "enseignement " + i + " : " + answers.Count + " reponses (2 a 4)");
                for (int a = 0; a < answers.Count; a++)
                    Assert.IsFalse(string.IsNullOrEmpty((string)answers[a]), "enseignement " + i + " : reponse " + a + " vide");

                int correct = (int)q["correct"];
                Assert.IsTrue(correct >= 0 && correct < answers.Count, "enseignement " + i + " : 'correct' = " + correct + " hors des reponses");
                Assert.IsFalse(string.IsNullOrEmpty((string)q["situation"]), "enseignement " + i + " : situation vide");
                Assert.IsFalse(string.IsNullOrEmpty((string)q["explain"]), "enseignement " + i + " : explication vide");
            }

            Assert.Greater(withQuestion, 0, "aucun enseignement ne porte de question");
        }

        // =================================================================
        //  PHOTOS, PICTOGRAMMES, SHADERS
        // =================================================================
        [Test]
        public void Photos_De_Chaque_Element_Presentes_Et_Importees_En_Sprite()
        {
            string[] names =
            {
                "Plain", "Desert", "Base", "Portal",
                "command_0", "command_1", "command_2",
                "bunker_0", "bunker_1", "bunker_2",
                "gas_0", "gas_1", "gas_2",
                "Crystal_0", "Crystal_1", "Crystal_2",
                "tank_1", "tank_2",
                "enemy_1", "enemy_2", "enemy_3",
                "portal_1", "portal_2"
            };

            List<string> missing = new List<string>();
            for (int i = 0; i < names.Length; i++)
                if (Resources.Load<Sprite>("Photos/" + names[i]) == null) missing.Add(names[i]);

            Assert.IsEmpty(missing, "photos absentes ou pas importees en Sprite (2D and UI) : " + string.Join(", ", missing.ToArray()));
        }

        [Test]
        public void ElementPhotos_Trouve_Chaque_Sujet_Par_Fichier()
        {
            Assert.IsNotNull(ElementPhotos.Get(PhotoSubject.Tank, 1));
            Assert.IsNotNull(ElementPhotos.Get(PhotoSubject.Tank, 2));
            Assert.IsNotNull(ElementPhotos.Get(PhotoSubject.Enemy, 1));
            Assert.IsNotNull(ElementPhotos.Get(PhotoSubject.Enemy, 2));
            Assert.IsNotNull(ElementPhotos.Get(PhotoSubject.Enemy, 3));
            for (int level = 0; level <= 2; level++)
            {
                Assert.IsNotNull(ElementPhotos.Get(PhotoSubject.Bunker, level), "bunker " + level);
                Assert.IsNotNull(ElementPhotos.Get(PhotoSubject.GasFactory, level), "gas " + level);
                Assert.IsNotNull(ElementPhotos.Get(PhotoSubject.CrystalFactory, level), "crystal " + level);
                Assert.IsNotNull(ElementPhotos.Get(PhotoSubject.CommandCenter, level), "command " + level);
            }
            Assert.IsNotNull(ElementPhotos.Get(PhotoSubject.Desert, 0));
            Assert.IsNotNull(ElementPhotos.Get(PhotoSubject.Plain, 0));
        }

        [Test]
        public void Pictogrammes_Generes_Pour_Chaque_Element()
        {
            foreach (MNLTHII.UI.IconKind kind in System.Enum.GetValues(typeof(MNLTHII.UI.IconKind)))
            {
                if (kind == MNLTHII.UI.IconKind.None) continue;
                Sprite s = IconLibrary.Get(kind);
                Assert.IsNotNull(s, "pictogramme " + kind);
                Assert.Greater(s.texture.width, 0);
            }
        }

        [Test]
        public void Shaders_Du_Projet_Presents()
        {
            foreach (string name in new[] { "Shaders/MNLTH_GroundDecal", "Shaders/MNLTH_PawnRim", "Shaders/MNLTH_BoardCalm" })
            {
                Shader shader = Resources.Load<Shader>(name);
                Assert.IsNotNull(shader, "Resources/" + name + " introuvable : traits de tir, bulles et pastilles invisibles");
                Assert.IsTrue(shader.isSupported, name + " non supporte sur cette plateforme");
            }
        }

        [Test]
        public void Habillage_UI_Present()
        {
            foreach (string name in new[] { "ui_plate_cyan", "ui_plate_gold", "ui_plate_red", "ui_button_cyan", "ui_button_gold", "ui_panel_dark", "ui_hexgrid" })
                Assert.IsNotNull(Resources.Load<Sprite>("UI/" + name), "Resources/UI/" + name + " manquant");
        }

        // =================================================================
        //  BUILD
        // =================================================================
        [Test]
        public void Scenes_Du_Build()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            Assert.GreaterOrEqual(scenes.Length, 2, "il faut le menu et le jeu dans le build");

            bool menu = false, game = false;
            for (int i = 0; i < scenes.Length; i++)
            {
                Assert.IsTrue(File.Exists(scenes[i].path), "scene du build absente du disque : " + scenes[i].path);
                if (scenes[i].enabled && scenes[i].path.EndsWith("MainMenu.unity")) menu = true;
                if (scenes[i].enabled && scenes[i].path.EndsWith("Game.unity")) game = true;
            }
            Assert.IsTrue(menu, "MainMenu.unity n'est pas activee dans le build");
            Assert.IsTrue(game, "Game.unity n'est pas activee dans le build");
            Assert.IsTrue(scenes[0].path.EndsWith("MainMenu.unity"), "la premiere scene du build doit etre le menu");
        }

        [Test]
        public void Aucun_Script_Manquant_Dans_Les_Scenes_Du_Build()
        {
            // Un composant dont le script a disparu ("Missing (Mono Script)") ne fait
            // aucun bruit en Play, mais tout ce qu'il pilotait ne marche plus.
            System.Text.RegularExpressions.Regex rx =
                new System.Text.RegularExpressions.Regex(@"m_Script: \{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: 3\}");
            List<string> bad = new List<string>();

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (!scenes[i].enabled || !File.Exists(scenes[i].path)) continue;
                string text = File.ReadAllText(scenes[i].path);

                if (text.Contains("m_Script: {fileID: 0}"))
                    bad.Add(scenes[i].path + " : un composant sans script (fileID 0)");

                HashSet<string> seen = new HashSet<string>();
                foreach (System.Text.RegularExpressions.Match m in rx.Matches(text))
                {
                    string guid = m.Groups[2].Value;
                    if (!seen.Add(guid)) continue;
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path)) bad.Add(scenes[i].path + " : script introuvable pour le guid " + guid);
                }
            }

            Assert.IsEmpty(bad, string.Join("\n", bad.ToArray()));
        }

        [Test]
        public void Aucun_Caractere_Non_ASCII_Dans_Les_Scripts()
        {
            // L'hebreu vit dans Resources/*.json, jamais dans un .cs : un seul accent
            // dans un fichier mal encode et Unity affiche des points d'interrogation.
            string root = Path.Combine(Application.dataPath, "Scripts");
            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            List<string> bad = new List<string>();

            for (int i = 0; i < files.Length; i++)
            {
                byte[] bytes = File.ReadAllBytes(files[i]);
                for (int b = 0; b < bytes.Length; b++)
                {
                    if (bytes[b] > 127)
                    {
                        bad.Add(files[i].Substring(root.Length + 1) + " (octet " + b + ")");
                        break;
                    }
                }
            }

            Assert.IsEmpty(bad, "caracteres non-ASCII dans : " + string.Join(", ", bad.ToArray()));
        }

        [Test]
        public void Le_TestRunner_De_Scene_Nest_Pas_Dans_Le_Build()
        {
            // TestRunner.cs ecrit dans la console a chaque lancement : il n'a rien a faire
            // dans une scene de production. On verifie qu'aucune scene du build ne le porte.
            string[] guids = AssetDatabase.FindAssets("t:MonoScript TestRunner");
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (!scenes[i].enabled || !File.Exists(scenes[i].path)) continue;
                string text = File.ReadAllText(scenes[i].path);
                for (int g = 0; g < guids.Length; g++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[g]);
                    if (!path.EndsWith("/TestRunner.cs")) continue;
                    Assert.IsFalse(text.Contains(guids[g]), scenes[i].path + " contient encore le composant TestRunner (a retirer pour la prod)");
                }
            }
        }

        // =================================================================
        //  HEBREU : chiffres et latin dans un texte RTL
        // =================================================================
        [Test]
        public void Rtl_Inverse_Les_Suites_Latines_Seulement()
        {
            Assert.AreEqual("", Rtl.Fix(""));
            Assert.IsNull(Rtl.Fix(null));
            Assert.AreEqual("\u05e9\u05dc\u05d5\u05dd", Rtl.Fix("\u05e9\u05dc\u05d5\u05dd"), "hebreu pur : inchange");
            Assert.AreEqual("04", Rtl.Fix("40"), "un nombre est retourne pour que TMP le remette a l'endroit");
            Assert.AreEqual("\u05e9\u05dc\u05d5\u05dd 04", Rtl.Fix("\u05e9\u05dc\u05d5\u05dd 40"));
            Assert.AreEqual("4/3", Rtl.Fix("3/4"), "le separateur interne suit la suite");
            Assert.AreEqual("cba \u05d0", Rtl.Fix("abc \u05d0"));
            Assert.AreEqual(Rtl.Fix(Rtl.Fix("abc 12")).Length, "abc 12".Length, "Fix est une involution sur la longueur");
        }

        // =================================================================
        //  SAUVEGARDE : le fichier fait l'aller-retour JSON sans rien perdre
        // =================================================================
        [Test]
        public void SaveGameFile_AllerRetour_JSON()
        {
            SaveGameFile file = new SaveGameFile();
            file.id = "partie_test";
            file.turn = 17;
            file.difficulty = 2;
            file.map = 3;
            file.energy = 123;
            file.baseHP = 150;
            file.baseMaxHP = 200;
            file.portalsAlive = 4;
            file.portalsTotal = 6;
            file.tanks = 3;
            file.foes = 5;
            file.hasSurge = true;
            file.surgeQ = 7; file.surgeR = -7;
            file.hexes.Add(new SavedHex { q = 1, r = -1, t = (int)TypeOfHex.gas, l = 2, hp = 30 });
            file.pawns.Add(new SavedPawn { q = 2, r = -2, foe = true, lvl = 2, hp = 33, st = 1 });
            file.portals.Add(new SavedPortal { q = 7, r = -7, kills = 2, shieldDown = 0, calm = 1 });

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(file);
            SaveGameFile back = Newtonsoft.Json.JsonConvert.DeserializeObject<SaveGameFile>(json);

            Assert.AreEqual(file.id, back.id);
            Assert.AreEqual(17, back.turn);
            Assert.AreEqual(2, back.difficulty);
            Assert.AreEqual(3, back.map);
            Assert.AreEqual(123, back.energy);
            Assert.AreEqual(150, back.baseHP);
            Assert.AreEqual(4, back.portalsAlive);
            Assert.AreEqual(3, back.tanks);
            Assert.AreEqual(5, back.foes);
            Assert.IsTrue(back.hasSurge);
            Assert.AreEqual(1, back.hexes.Count);
            Assert.AreEqual((int)TypeOfHex.gas, back.hexes[0].t);
            Assert.AreEqual(2, back.hexes[0].l);
            Assert.AreEqual(1, back.pawns.Count);
            Assert.IsTrue(back.pawns[0].foe);
            Assert.AreEqual(33, back.pawns[0].hp);
            Assert.AreEqual(1, back.portals.Count);
            Assert.AreEqual(2, back.portals[0].kills);
        }
    }
}
#endif
