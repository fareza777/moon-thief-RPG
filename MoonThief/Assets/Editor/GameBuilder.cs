using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MoonThief.EditorTools
{
    /// <summary>
    /// Headless builder for THE MOON THIEF. Creates the scene from code, renders still frames
    /// straight out of the game camera (title, village, dialog, fields, battle, forest, ending),
    /// then builds an Android APK and a Windows preview executable.
    ///
    /// Headless:  Unity.exe -batchmode -projectPath &lt;proj&gt; -executeMethod MoonThief.EditorTools.GameBuilder.BuildBatch -quit
    /// </summary>
    public static class GameBuilder
    {
        const string ScenePath = "Assets/Scenes/Game.unity";
        const string ApkPath = "Build/Android/TheMoonThief.apk";
        const string WinPath = "Build/Windows/TheMoonThief.exe";
        const string LinuxPath = "Build/Linux/TheMoonThief.x86_64";
        const string AppId = "com.fajargames.moonthief";

        [MenuItem("MoonThief/Build Game")]
        public static void BuildFromMenu() => Build(renderScreenshots: true, buildPlayers: true);

        public static void BuildBatch()
        {
            Build(renderScreenshots: true, buildPlayers: true);
            EditorApplication.Exit(0);
        }

        public static void ScreenshotsOnly()
        {
            Build(renderScreenshots: true, buildPlayers: false);
            EditorApplication.Exit(0);
        }

        /// <summary>Player-only rebuild: used while iterating on runtime behaviour.</summary>
        public static void WindowsOnly()
        {
            Build(renderScreenshots: false, buildPlayers: false);
            BuildWindows();
            EditorApplication.Exit(0);
        }

        /// <summary>The APK on its own. BuildBatch does both players in one process, which is
        /// slower than a batch-mode command may run; this is the Android half of it.</summary>
        public static void AndroidOnly()
        {
            Build(renderScreenshots: false, buildPlayers: false);
            BuildAndroid();
            EditorApplication.Exit(0);
        }

        /// <summary>A Linux player - the build this box can run headless for the -selftest pass.</summary>
        public static void LinuxOnly()
        {
            Build(renderScreenshots: false, buildPlayers: false);
            BuildLinux();
            EditorApplication.Exit(0);
        }

        static string Proj(string relative) => Path.Combine(Directory.GetParent(Application.dataPath).FullName, relative);

        static void Build(bool renderScreenshots, bool buildPlayers)
        {
            Debug.Log("[MoonThief] creating scene");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var go = new GameObject("Game");
            var game = go.AddComponent<Game>();
            game.EditorMode = true;
            game.BuildAll(16f);

            if (renderScreenshots) RenderScreenshots(game);

            // the editor instance holds runtime-created textures in its fields; shipping it
            // produced a scene the player refused to load. Ship a pristine scene instead:
            // one bare Game object that rebuilds the whole hierarchy in Start().
            // Every root created outside the Game host (dumps, leftovers, editor tools)
            // must go too - anything left behind bloats level0 with dangling texture refs.
            Object.DestroyImmediate(go);
            for (int i = scene.rootCount - 1; i >= 0; i--)
            {
                var rootGo = scene.GetRootGameObjects()[i];
                Object.DestroyImmediate(rootGo);
            }
            var boot = new GameObject("Game");
            boot.AddComponent<Game>();

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[MoonThief] scene saved to " + ScenePath);

            ConfigurePlayerSettings();

            if (!buildPlayers) return;
            BuildAndroid();
            BuildWindows();
        }

        // ------------------------------------------------------------------ screenshots

        /// <summary>One line per row of the village, so a wall of solid tiles is visible as text
        /// instead of guessed at from a screenshot: '.' grass, 'p' path, 'T' tree, '#' solid.</summary>
        static void DumpWalkGrid(int y0, int y1, int x0, int x1)
        {
            var map = GameMap.Build(1);
            for (int y = y0; y <= y1; y++)
            {
                var row = new System.Text.StringBuilder();
                for (int x = x0; x <= x1; x++)
                {
                    var g = map.At(new Vector2Int(x, y));
                    if (!map.Walkable(new Vector2Int(x, y))) row.Append(g == Ground.Tree ? 'T' : '#');
                    else row.Append(g == Ground.Path ? 'p' : g == Ground.Floor ? 'f' : '.');
                }
                Debug.Log("[GRID] y=" + y.ToString("00") + " x" + x0 + ".." + x1 + "  " + row);
            }
        }

        /// <summary>Batch entry: print the village and road walkability and quit. Used to check
        /// for invisible walls without rendering anything.</summary>
        public static void DumpGrid()
        {
            Debug.Log("[GRID] chapter 1, village and lane (x16..34)");
            DumpWalkGrid(6, 20, 16, 34);
            Debug.Log("[GRID] chapter 1, fields lane (x26..34)");
            DumpWalkGrid(30, 44, 26, 34);
        }

        static void RenderScreenshots(Game game)
        {
            DumpWalkGrid(6, 20, 24, 36);
            var dir = Proj("Screenshots");
            Directory.CreateDirectory(dir);
            int n = 0;
            void Shot(string name)
            {
                n++;
                game.RenderToPng(Path.Combine(dir, name + ".png"));
                AuditOverlaps(name, 16f, game.Cam != null ? game.Cam.transform.localPosition : Vector3.zero);
                AuditSprites(name);
            }

            // 1. the studio card
            game.EditorSplash();
            Shot("01-splash");

            // 2. the main menu over the title art
            game.EditorMenu();
            Shot("02-menu");

            // 3. settings
            game.EditorSettings();
            Shot("03-settings");

            // 4. credits
            game.EditorCredits();
            Shot("04-credits");

            // 5. the story slides
            game.EditorCinema(5);
            Shot("05-story");

            // 6. the night card between chapters
            game.EditorChapterCard(2);
            Shot("06-nightcard");

            // 7. village of chapter 1: hero by the crystal, NPCs, houses, lamp glow
            game.EditorExplore(1);
            Shot("07-village");

            // 8. dialog with Mira, the quest giver
            game.EditorDialog();
            Shot("08-dialog");

            // 9. the plaza market corner (props, critters, lamps)
            game.EditorCloseDialog();
            game.EditorPlaceHero(new Vector2(24.5f, 9.5f));
            Shot("09-plaza");

            // 10. out in the fields: crops, wild monsters, the road north
            game.EditorPlaceHero(new Vector2(30.5f, 38.5f));
            Shot("10-fields");

            // 11. battle, command phase with the 2x2 menu visible
            game.EditorBattle();
            Shot("11-battle-command");

            // 12. battle, mid fight with numbers and damage
            game.EditorBattleMid();
            Shot("12-battle-action");

            // 13. victory card
            game.EditorWinCard();
            Shot("13-battle-card");

            // 14. the pause card
            game.BackToExplore();
            game.EditorExplore(1);
            game.EditorPause();
            Shot("14-pause");

            // 15. chapter 3 forest with the guard waiting at the top
            game.EditorExplore(3);
            game.EditorPlaceHero(new Vector2(30.5f, 68.5f));
            Shot("15-forest");

            // 16. the ending: the moon comes back
            game.EditorEnding();
            Shot("16-ending");

            // 17-18. inside two of the six houses. The rooms are their own maps, so this is the
            // only way to see the masonry, the furniture and the doormat without walking there.
            game.EditorInterior(0);
            Shot("17-interior-shrine");
            game.EditorInterior(5);
            Shot("18-interior-kitchen");

            // the first-boot cards and Marn's shop card
            game.EditorOnboard();
            Shot("27-onboard");
            game.EditorExplore(1);
            game.EditorShop();
            Shot("28-shop");

            // 19-22. the journal: the hub, two pages and the bestiary
            game.EditorJournal(-1);
            Shot("19-journal-hub");
            game.EditorJournal(0);
            Shot("20-journal-character");
            game.EditorJournal(4);
            Shot("21-journal-quests");
            game.EditorJournal(3);
            Shot("22-bestiary");

            // 23-25: the three places text used to collide on a device -- a quest toast landing on
            // the dialog box, a loot toast over the HUD, and the night banner over the hero. These
            // come last on purpose: the talk frame leaves a dialog box open on the stage.
            game.EditorTalk();
            Shot("23-dialog-toast");
            game.EditorLootToast();
            Shot("24-loot-toast");
            game.EditorBanner();
            Shot("25-zone-banner");

            // 26. standing at a chest. It is the one collectible no other frame shows, and the
            // chest sheet was sliced 24 wide for a year - four columns of the next chest came with
            // every frame and every chest in the world was drawn with half a box beside it.
            game.EditorCloseDialog();
            game.EditorExplore(1);
            game.EditorPlaceHero(new Vector2(12.5f, 21.4f));
            Shot("26-chest");

            Debug.Log("[MoonThief] " + n + " screenshots written to " + dir);
        }

        // ------------------------------------------------------------------ layout audit

        /// <summary>
        /// Writes the layout lines the offline audit reads back: every text block, every UI
        /// plate and every character sprite with its frame pixels. Overlapping labels are the
        /// single worst text defect in a bitmap-font game and are invisible to the compiler, so
        /// the headless build measures them for every screen. The pass itself lives in
        /// LayoutAudit (a runtime script) so the player self-test measures the same things on
        /// the frames a phone would show.
        /// </summary>
        static void AuditOverlaps(string tag, float halfH, Vector3 cam) => LayoutAudit.Report(tag, halfH, cam);

        static void AuditSprites(string tag)
        {
            foreach (var sr in Object.FindObjectsOfType<SpriteRenderer>(true))
            {
                if (sr == null || sr.gameObject.name.IndexOf("pname", System.StringComparison.Ordinal) < 0
                    && sr.gameObject.name.IndexOf("pfill", System.StringComparison.Ordinal) < 0
                    && sr.gameObject.name.IndexOf("pbg", System.StringComparison.Ordinal) < 0
                    && sr.gameObject.name.IndexOf("ebg", System.StringComparison.Ordinal) < 0
                    && sr.gameObject.name.IndexOf("efill", System.StringComparison.Ordinal) < 0) continue;
                if (!sr.gameObject.activeInHierarchy) continue;
                var p = sr.transform.position;
                Debug.Log("[SPRITE] " + tag + "|" + sr.gameObject.name + "|on=" + sr.enabled
                          + "|mode=" + sr.drawMode + "|size=" + sr.size.x.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                          + "x" + sr.size.y.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                          + "|scl=" + sr.transform.lossyScale.x.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                          + "|a=" + sr.color.a.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                          + "|spr=" + (sr.sprite != null) + "|order=" + sr.sortingOrder
                          + "|at=" + p.x.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                          + "," + p.y.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        static string LabelPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null && parts.Count < 4) { parts.Insert(0, t.name); t = t.parent; }
            return string.Join("/", parts);
        }

        // ------------------------------------------------------------------ player settings

        static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "F7 Developer Games";
            PlayerSettings.productName = "The Moon Thief";
            PlayerSettings.bundleVersion = "1.0.0";

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.defaultScreenWidth = 576;
            PlayerSettings.defaultScreenHeight = 1024;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, AppId);

            // 25, not 24: Unity rejects anything below 25 with a LogError and then leaves the
            // setting alone, so the APK was never being built against the level this file asked
            // for - the minimum sat at whatever the project default happened to be.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.stripEngineCode = false;

            AssetDatabase.SaveAssets();
            Debug.Log("[MoonThief] player settings applied");
        }

        // ------------------------------------------------------------------ builds

        static void BuildAndroid()
        {
            var outPath = Proj(ApkPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            Debug.Log("[MoonThief] building Android APK -> " + outPath);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception("[MoonThief] Android build failed: " + report.summary.result +
                                           " (" + report.summary.totalErrors + " errors)");

            Debug.Log("[MoonThief] APK ready: " + outPath + "  size=" + new FileInfo(outPath).Length + " bytes");
        }

        static void BuildLinux()
        {
            var outPath = Proj(LinuxPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            Debug.Log("[MoonThief] building Linux player -> " + outPath);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outPath,
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
                Debug.LogWarning("[MoonThief] Linux build failed: " + report.summary.result);
            else
                Debug.Log("[MoonThief] Linux player ready: " + outPath);
        }

        static void BuildWindows()
        {
            var outPath = Proj(WinPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            Debug.Log("[MoonThief] building Windows preview -> " + outPath);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
                Debug.LogWarning("[MoonThief] Windows build failed: " + report.summary.result);
            else
                Debug.Log("[MoonThief] Windows preview ready: " + outPath);
        }
    }
}
