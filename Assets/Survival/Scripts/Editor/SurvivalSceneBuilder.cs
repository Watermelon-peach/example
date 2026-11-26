using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Survival.EditorTools
{
    /// <summary>
    /// Generates the whole Top-Down Survival scene (arena, prefabs, player, managers, UI).
    /// Re-run any time via Tools > Survival > Build Scene; it overwrites the generated assets.
    /// </summary>
    public static class SurvivalSceneBuilder
    {
        const string Root = "Assets/Survival";
        const string ScenePath = Root + "/Scenes/Survival.unity";
        const float ArenaHalf = 25f;

        [MenuItem("Tools/Survival/Build Scene")]
        public static void Build()
        {
            EnsureFolder("Assets", "Survival");
            EnsureFolder(Root, "Materials");
            EnsureFolder(Root, "Prefabs");
            EnsureFolder(Root, "Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Materials ---
            var mGround = Mat("Ground", new Color(0.20f, 0.22f, 0.25f));
            var mWall = Mat("Wall", new Color(0.32f, 0.36f, 0.44f));
            var mObstacle = Mat("Obstacle", new Color(0.46f, 0.42f, 0.36f));
            var mPlayer = Mat("Player", new Color(0.20f, 0.60f, 1.00f));
            var mEnemy = Mat("Enemy", new Color(0.90f, 0.20f, 0.20f));
            var mDark = Mat("Dark", new Color(0.08f, 0.08f, 0.10f));
            var mBullet = Mat("Bullet", new Color(1.00f, 0.85f, 0.25f), emissive: true);

            // --- Lighting ---
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

            // --- Arena (everything under Environment is baked into the NavMesh) ---
            var env = new GameObject("Environment");
            var ground = Primitive(PrimitiveType.Plane, "Ground", env.transform, Vector3.zero, new Vector3(ArenaHalf / 5f, 1f, ArenaHalf / 5f), mGround);
            ground.isStatic = true;

            float w = ArenaHalf + 0.5f, len = ArenaHalf * 2f + 2f;
            Primitive(PrimitiveType.Cube, "Wall_N", env.transform, new Vector3(0, 1, w), new Vector3(len, 2, 1), mWall);
            Primitive(PrimitiveType.Cube, "Wall_S", env.transform, new Vector3(0, 1, -w), new Vector3(len, 2, 1), mWall);
            Primitive(PrimitiveType.Cube, "Wall_E", env.transform, new Vector3(w, 1, 0), new Vector3(1, 2, len), mWall);
            Primitive(PrimitiveType.Cube, "Wall_W", env.transform, new Vector3(-w, 1, 0), new Vector3(1, 2, len), mWall);

            var obstacles = new (Vector3 pos, Vector3 size, float yaw)[]
            {
                (new Vector3(-9, 1, 8), new Vector3(4, 2, 1.5f), 20f),
                (new Vector3(10, 1, 9), new Vector3(2, 2, 2), 45f),
                (new Vector3(8, 1, -10), new Vector3(5, 2, 1.5f), -15f),
                (new Vector3(-11, 1, -8), new Vector3(2.5f, 2, 2.5f), 10f),
                (new Vector3(0, 1, 15), new Vector3(6, 2, 1.2f), 0f),
                (new Vector3(0, 1, -16), new Vector3(1.5f, 2, 5), 0f),
                (new Vector3(17, 1, 0), new Vector3(1.2f, 2, 6), 0f),
                (new Vector3(-17, 1, 2), new Vector3(1.2f, 2, 5), 30f),
            };
            for (int i = 0; i < obstacles.Length; i++)
            {
                var o = Primitive(PrimitiveType.Cube, $"Obstacle_{i}", env.transform, obstacles[i].pos, obstacles[i].size, mObstacle);
                o.transform.rotation = Quaternion.Euler(0, obstacles[i].yaw, 0);
            }

            var surface = env.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            env.AddComponent<RuntimeNavMeshBaker>();

            // --- Bullet prefab ---
            var bulletGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulletGo.name = "Bullet";
            Object.DestroyImmediate(bulletGo.GetComponent<Collider>());
            bulletGo.transform.localScale = Vector3.one * 0.3f;
            var bulletRenderer = bulletGo.GetComponent<Renderer>();
            bulletRenderer.sharedMaterial = mBullet;
            bulletRenderer.shadowCastingMode = ShadowCastingMode.Off;
            bulletGo.AddComponent<Bullet>();
            var bulletPrefab = SavePrefab(bulletGo, "Bullet").GetComponent<Bullet>();

            // --- Enemy prefab ---
            var enemyGo = new GameObject("Enemy");
            var enemyBody = Primitive(PrimitiveType.Capsule, "Body", enemyGo.transform, new Vector3(0, 1, 0), Vector3.one, mEnemy, keepCollider: false);
            Primitive(PrimitiveType.Cube, "Visor", enemyGo.transform, new Vector3(0, 1.45f, 0.42f), new Vector3(0.7f, 0.18f, 0.2f), mDark, keepCollider: false);
            var enemyCol = enemyGo.AddComponent<CapsuleCollider>();
            enemyCol.center = new Vector3(0, 1, 0);
            enemyCol.height = 2f;
            enemyCol.radius = 0.5f;
            var agent = enemyGo.AddComponent<NavMeshAgent>();
            agent.radius = 0.5f;
            agent.height = 2f;
            agent.angularSpeed = 720f;
            agent.stoppingDistance = 1.1f;
            agent.autoBraking = false;
            var enemy = enemyGo.AddComponent<Enemy>();
            enemy.bodyRenderer = enemyBody.GetComponent<Renderer>();
            var enemyPrefab = SavePrefab(enemyGo, "Enemy").GetComponent<Enemy>();

            // --- Player ---
            var player = new GameObject("Player") { tag = "Player" };
            player.transform.position = new Vector3(0, 0.05f, 0);
            var playerBody = Primitive(PrimitiveType.Capsule, "Body", player.transform, new Vector3(0, 1, 0), Vector3.one, mPlayer, keepCollider: false);
            Primitive(PrimitiveType.Cube, "Gun", player.transform, new Vector3(0, 1.2f, 0.65f), new Vector3(0.22f, 0.22f, 0.8f), mDark, keepCollider: false);
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(player.transform, false);
            muzzle.localPosition = new Vector3(0, 1.2f, 1.15f);
            var cc = player.AddComponent<CharacterController>();
            cc.center = new Vector3(0, 1, 0);
            cc.height = 2f;
            cc.radius = 0.5f;
            var playerHealth = player.AddComponent<PlayerHealth>();
            playerHealth.bodyRenderer = playerBody.GetComponent<Renderer>();
            var playerController = player.AddComponent<PlayerController>();
            playerController.bulletPrefab = bulletPrefab;
            playerController.muzzle = muzzle;

            // --- Camera ---
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<CameraFollow>();
            follow.target = player.transform;
            follow.SnapToTarget();

            // --- Managers ---
            var managers = new GameObject("GameManager");
            var spawner = managers.AddComponent<WaveSpawner>();
            spawner.enemyPrefab = enemyPrefab;
            spawner.player = player.transform;
            spawner.arenaHalfSize = new Vector2(ArenaHalf - 2f, ArenaHalf - 2f);
            var gm = managers.AddComponent<GameManager>();
            gm.player = playerHealth;
            gm.spawner = spawner;

            // --- UI ---
            gm.hud = BuildHud(playerHealth, spawner);
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // --- Save & register ---
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Survival] Scene built at {ScenePath}");
        }

        // ---------------- UI ----------------

        static HUD BuildHud(PlayerHealth playerHealth, WaveSpawner spawner)
        {
            var canvasGo = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var canvas = canvasGo.transform;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var hud = canvasGo.AddComponent<HUD>();
            hud.player = playerHealth;
            hud.spawner = spawner;

            // Health bar (top-left)
            var barBg = Rect("HealthBar", canvas);
            Place(barBg, new Vector2(0, 1), new Vector2(30, -30), new Vector2(440, 38));
            Img(barBg, new Color(0, 0, 0, 0.6f));
            var fill = Rect("Fill", barBg);
            Stretch(fill, 4f);
            Img(fill, new Color(0.88f, 0.22f, 0.28f));
            hud.healthFill = fill;
            var hpText = Rect("HealthText", barBg);
            Stretch(hpText);
            hud.healthText = Txt(hpText, "100 / 100", 24, TextAnchor.MiddleCenter, font);

            // Wave (top-center)
            var wave = Rect("WaveText", canvas);
            Place(wave, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(500, 70));
            hud.waveText = Txt(wave, "WAVE 1", 46, TextAnchor.MiddleCenter, font, FontStyle.Bold);

            // Remaining / kills (top-right)
            var remaining = Rect("RemainingText", canvas);
            Place(remaining, new Vector2(1, 1), new Vector2(-30, -26), new Vector2(400, 40));
            hud.remainingText = Txt(remaining, "남은 적  0", 28, TextAnchor.MiddleRight, font);
            var kills = Rect("KillsText", canvas);
            Place(kills, new Vector2(1, 1), new Vector2(-30, -68), new Vector2(400, 40));
            hud.killsText = Txt(kills, "처치  0", 28, TextAnchor.MiddleRight, font);

            // Wave announcement (center)
            var announce = Rect("AnnounceText", canvas);
            Place(announce, new Vector2(0.5f, 0.5f), new Vector2(0, 180), new Vector2(900, 160));
            hud.announceText = Txt(announce, "WAVE 1", 110, TextAnchor.MiddleCenter, font, FontStyle.Bold);
            hud.announceText.color = new Color(1f, 0.85f, 0.3f);

            // Game over panel
            var panel = Rect("GameOverPanel", canvas);
            Stretch(panel);
            Img(panel, new Color(0, 0, 0, 0.78f));
            hud.gameOverPanel = panel.gameObject;

            var title = Rect("Title", panel);
            Place(title, new Vector2(0.5f, 0.5f), new Vector2(0, 160), new Vector2(1000, 150));
            Txt(title, "GAME OVER", 120, TextAnchor.MiddleCenter, font, FontStyle.Bold).color = new Color(1f, 0.3f, 0.3f);

            var stats = Rect("Stats", panel);
            Place(stats, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(1000, 120));
            hud.gameOverStats = Txt(stats, "", 40, TextAnchor.MiddleCenter, font);

            var button = Rect("RestartButton", panel);
            Place(button, new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(320, 80));
            var buttonImage = Img(button, new Color(0.95f, 0.75f, 0.2f));
            hud.restartButton = button.gameObject.AddComponent<Button>();
            hud.restartButton.targetGraphic = buttonImage;
            var buttonLabel = Rect("Label", button);
            Stretch(buttonLabel);
            Txt(buttonLabel, "다시 시작", 36, TextAnchor.MiddleCenter, font, FontStyle.Bold).color = new Color(0.1f, 0.1f, 0.1f);

            return hud;
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        static Image Img(RectTransform rt, Color color)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        static Text Txt(RectTransform rt, string text, int size, TextAnchor align, Font font, FontStyle style = FontStyle.Normal)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.text = text;
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = Color.white;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(2, -2);
            return t;
        }

        // ---------------- Helpers ----------------

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool keepCollider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static Material Mat(string name, Color color, bool emissive = false)
        {
            string path = $"{Root}/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            if (emissive)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 2.5f);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static GameObject SavePrefab(GameObject go, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Root}/Prefabs/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
