using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Survival.EditorTools
{
    /// <summary>
    /// Generates the whole Top-Down Survival scene (arena, prefabs, player, managers, effects, UI).
    /// Re-run any time via Tools > Survival > Build Scene; it overwrites the generated assets.
    /// </summary>
    public static class SurvivalSceneBuilder
    {
        #region 변수

        const string Root = "Assets/Survival";
        const string ScenePath = Root + "/Scenes/Survival.unity";
        const float ArenaHalf = 25f;

        static Font font;

        #endregion

        #region 커스텀 메서드

        [MenuItem("Tools/Survival/Build Scene")]
        public static void Build()
        {
            EnsureFolder("Assets", "Survival");
            EnsureFolder(Root, "Materials");
            EnsureFolder(Root, "Prefabs");
            EnsureFolder(Root, "Scenes");
            EnsureFolder(Root, "Settings");
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Materials ---
            var mGround = Mat("Ground", new Color(0.20f, 0.22f, 0.25f));
            var mWall = Mat("Wall", new Color(0.32f, 0.36f, 0.44f));
            var mObstacle = Mat("Obstacle", new Color(0.46f, 0.42f, 0.36f));
            var mPlayer = Mat("Player", new Color(0.20f, 0.60f, 1.00f));
            var mEnemy = Mat("Enemy", new Color(0.90f, 0.20f, 0.20f));
            var mDark = Mat("Dark", new Color(0.08f, 0.08f, 0.10f));
            var mBullet = Mat("Bullet", new Color(1.00f, 0.85f, 0.25f), emission: 2.5f);
            var mEnemyBullet = Mat("EnemyBullet", new Color(1.00f, 0.25f, 0.75f), emission: 3f);
            var mPickup = Mat("Pickup", new Color(0.30f, 1.00f, 0.45f), emission: 2f);
            var mParticles = ParticleMat("Sparks");

            // --- Lighting & post-processing ---
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            BuildVolume();

            // --- Arena (everything under Environment is baked into the NavMesh) ---
            var env = new GameObject("Environment");
            Primitive(PrimitiveType.Plane, "Ground", env.transform, Vector3.zero, new Vector3(ArenaHalf / 5f, 1f, ArenaHalf / 5f), mGround);

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

            // --- Prefabs ---
            var bulletPrefab = SavePrefab(Projectile("Bullet", 0.3f, mBullet).AddComponent<Bullet>().gameObject, "Bullet").GetComponent<Bullet>();
            var enemyBulletPrefab = SavePrefab(Projectile("EnemyBullet", 0.45f, mEnemyBullet).AddComponent<EnemyProjectile>().gameObject, "EnemyBullet").GetComponent<EnemyProjectile>();

            var pickupGo = new GameObject("HealthPickup");
            var gem = Primitive(PrimitiveType.Cube, "Gem", pickupGo.transform, Vector3.zero, Vector3.one * 0.45f, mPickup, keepCollider: false);
            gem.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            pickupGo.AddComponent<Pickup>();
            var pickupPrefab = SavePrefab(pickupGo, "HealthPickup").GetComponent<Pickup>();

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
            enemyGo.AddComponent<Enemy>().bodyRenderer = enemyBody.GetComponent<Renderer>();
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
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
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
            var hud = BuildHud(playerHealth, playerController, spawner, out var upgrades);
            gm.hud = hud;
            gm.upgrades = upgrades;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // --- Effects ---
            var fxGo = new GameObject("Effects");
            var fx = fxGo.AddComponent<Effects>();
            fx.sparks = BuildSparks(fxGo.transform, mParticles);
            fx.enemyProjectilePrefab = enemyBulletPrefab;
            fx.pickupPrefab = pickupPrefab;
            fx.damageCanvas = hud.GetComponent<Canvas>();
            fx.cameraFollow = follow;
            fx.player = player.transform;
            fx.playerHealth = playerHealth;

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

        // ---------------- Effects ----------------

        static ParticleSystem BuildSparks(Transform parent, Material mat)
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;          // always "playing" so Emit() particles simulate
            main.playOnAwake = true;
            main.duration = 1f;
            main.startLifetime = 0.5f;
            main.startSpeed = 0f;
            main.gravityModifier = 1.2f;
            main.maxParticles = 3000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = false;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        static void BuildVolume()
        {
            string path = $"{Root}/Settings/SurvivalVolume.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(1.3f);
            bloom.threshold.Override(0.95f);
            bloom.scatter.Override(0.65f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.3f);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);

            foreach (var component in profile.components)
            {
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);

            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        // ---------------- UI ----------------

        static HUD BuildHud(PlayerHealth playerHealth, PlayerController playerController, WaveSpawner spawner, out UpgradeSystem upgrades)
        {
            var canvasGo = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var canvas = canvasGo.transform;

            var hud = canvasGo.AddComponent<HUD>();
            hud.player = playerHealth;
            hud.playerController = playerController;
            hud.spawner = spawner;

            // Red damage flash (first child: drawn behind everything else)
            var overlay = Rect("DamageOverlay", canvas);
            Stretch(overlay);
            hud.damageOverlay = Img(overlay, new Color(1f, 0.1f, 0.1f, 0f));
            hud.damageOverlay.raycastTarget = false;

            // Health + dash bars (top-left)
            var barBg = Rect("HealthBar", canvas);
            Place(barBg, new Vector2(0, 1), new Vector2(30, -30), new Vector2(440, 38));
            Img(barBg, new Color(0, 0, 0, 0.6f));
            var fill = Rect("Fill", barBg);
            Stretch(fill, 4f);
            Img(fill, new Color(0.88f, 0.22f, 0.28f));
            hud.healthFill = fill;
            var hpText = Rect("HealthText", barBg);
            Stretch(hpText);
            hud.healthText = Txt(hpText, "100 / 100", 24, TextAnchor.MiddleCenter);

            var dashBg = Rect("DashBar", canvas);
            Place(dashBg, new Vector2(0, 1), new Vector2(30, -74), new Vector2(440, 14));
            Img(dashBg, new Color(0, 0, 0, 0.6f));
            var dashFill = Rect("Fill", dashBg);
            Stretch(dashFill, 3f);
            hud.dashFillImage = Img(dashFill, new Color(0.35f, 0.85f, 1f));
            hud.dashFill = dashFill;
            var dashLabel = Rect("DashLabel", canvas);
            Place(dashLabel, new Vector2(0, 1), new Vector2(30, -92), new Vector2(440, 30));
            Txt(dashLabel, "대시  마우스 우클릭  ·  사격 자동", 20, TextAnchor.MiddleLeft).color = new Color(1f, 1f, 1f, 0.7f);

            // Wave (top-center)
            var wave = Rect("WaveText", canvas);
            Place(wave, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(500, 70));
            hud.waveText = Txt(wave, "WAVE 1", 46, TextAnchor.MiddleCenter, FontStyle.Bold);

            // Boss bar (under wave text)
            var bossBar = Rect("BossBar", canvas);
            Place(bossBar, new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(820, 28));
            Img(bossBar, new Color(0, 0, 0, 0.65f));
            var bossFill = Rect("Fill", bossBar);
            Stretch(bossFill, 4f);
            Img(bossFill, new Color(0.9f, 0.1f, 0.6f));
            hud.bossFill = bossFill;
            var bossLabel = Rect("BossLabel", bossBar);
            Place(bossLabel, new Vector2(0.5f, 1), new Vector2(0, 34), new Vector2(400, 34));
            Txt(bossLabel, "BOSS", 28, TextAnchor.MiddleCenter, FontStyle.Bold).color = new Color(1f, 0.45f, 0.75f);
            hud.bossBar = bossBar.gameObject;

            // Remaining / kills / best (top-right)
            hud.remainingText = Corner("RemainingText", canvas, -26, "남은 적  0", 28);
            hud.killsText = Corner("KillsText", canvas, -68, "처치  0", 28);
            hud.bestText = Corner("BestText", canvas, -106, "", 22);
            hud.bestText.color = new Color(1f, 0.85f, 0.4f, 0.85f);

            // Wave announcement (center)
            var announce = Rect("AnnounceText", canvas);
            Place(announce, new Vector2(0.5f, 0.5f), new Vector2(0, 180), new Vector2(900, 200));
            hud.announceText = Txt(announce, "WAVE 1", 110, TextAnchor.MiddleCenter, FontStyle.Bold);
            hud.announceText.color = new Color(1f, 0.85f, 0.3f);

            // Upgrade panel
            upgrades = BuildUpgradePanel(canvas, playerController, playerHealth);

            // Game over panel
            var panel = Rect("GameOverPanel", canvas);
            Stretch(panel);
            Img(panel, new Color(0, 0, 0, 0.78f));
            hud.gameOverPanel = panel.gameObject;

            var title = Rect("Title", panel);
            Place(title, new Vector2(0.5f, 0.5f), new Vector2(0, 190), new Vector2(1000, 150));
            Txt(title, "GAME OVER", 120, TextAnchor.MiddleCenter, FontStyle.Bold).color = new Color(1f, 0.3f, 0.3f);

            var stats = Rect("Stats", panel);
            Place(stats, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1100, 170));
            hud.gameOverStats = Txt(stats, "", 40, TextAnchor.MiddleCenter);

            var button = Rect("RestartButton", panel);
            Place(button, new Vector2(0.5f, 0.5f), new Vector2(0, -130), new Vector2(320, 80));
            var buttonImage = Img(button, new Color(0.95f, 0.75f, 0.2f));
            hud.restartButton = button.gameObject.AddComponent<Button>();
            hud.restartButton.targetGraphic = buttonImage;
            var buttonLabel = Rect("Label", button);
            Stretch(buttonLabel);
            Txt(buttonLabel, "다시 시작", 36, TextAnchor.MiddleCenter, FontStyle.Bold).color = new Color(0.1f, 0.1f, 0.1f);

            return hud;
        }

        static UpgradeSystem BuildUpgradePanel(Transform canvas, PlayerController playerController, PlayerHealth playerHealth)
        {
            var panel = Rect("UpgradePanel", canvas);
            Stretch(panel);
            Img(panel, new Color(0.02f, 0.03f, 0.06f, 0.75f));

            var header = Rect("Header", panel);
            Place(header, new Vector2(0.5f, 0.5f), new Vector2(0, 260), new Vector2(1000, 100));
            Txt(header, "웨이브 클리어! 업그레이드 선택", 60, TextAnchor.MiddleCenter, FontStyle.Bold).color = new Color(1f, 0.85f, 0.3f);

            var upgrades = panel.gameObject.AddComponent<UpgradeSystem>();
            upgrades.player = playerController;
            upgrades.playerHealth = playerHealth;
            upgrades.panel = panel.gameObject;
            upgrades.cards = new Button[3];
            upgrades.titles = new Text[3];
            upgrades.descriptions = new Text[3];

            for (int i = 0; i < 3; i++)
            {
                var card = Rect($"Card{i + 1}", panel);
                Place(card, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 440f, -20f), new Vector2(400, 320));
                var cardImage = Img(card, new Color(0.13f, 0.15f, 0.21f, 0.97f));
                var button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = cardImage;
                var colors = button.colors;
                colors.highlightedColor = new Color(1.25f, 1.25f, 1.4f);
                colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
                button.colors = colors;
                upgrades.cards[i] = button;

                var accent = Rect("Accent", card);
                accent.anchorMin = new Vector2(0, 1);
                accent.anchorMax = new Vector2(1, 1);
                accent.pivot = new Vector2(0.5f, 1);
                accent.sizeDelta = new Vector2(0, 8);
                accent.anchoredPosition = Vector2.zero;
                Img(accent, new Color(1f, 0.8f, 0.25f)).raycastTarget = false;

                var t = Rect("Title", card);
                Place(t, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(380, 70));
                upgrades.titles[i] = Txt(t, "업그레이드", 44, TextAnchor.MiddleCenter, FontStyle.Bold);
                upgrades.titles[i].color = new Color(1f, 0.88f, 0.45f);

                var d = Rect("Description", card);
                Place(d, new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(360, 120));
                upgrades.descriptions[i] = Txt(d, "설명", 30, TextAnchor.MiddleCenter);

                var k = Rect("Key", card);
                Place(k, new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(200, 44));
                Txt(k, $"[ {i + 1} ]", 30, TextAnchor.MiddleCenter, FontStyle.Bold).color = new Color(1f, 1f, 1f, 0.5f);
            }
            return upgrades;
        }

        static Text Corner(string name, Transform canvas, float y, string text, int size)
        {
            var rt = Rect(name, canvas);
            Place(rt, new Vector2(1, 1), new Vector2(-30, y), new Vector2(400, 40));
            return Txt(rt, text, size, TextAnchor.MiddleRight);
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

        static Text Txt(RectTransform rt, string text, int size, TextAnchor align, FontStyle style = FontStyle.Normal)
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
            rt.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2, -2);
            return t;
        }

        // ---------------- Helpers ----------------

        static GameObject Projectile(string name, float size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.localScale = Vector3.one * size;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

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

        static Material Mat(string name, Color color, float emission = 0f)
        {
            var mat = LoadOrCreateMat(name, "Universal Render Pipeline/Lit");
            mat.SetColor("_BaseColor", color);
            if (emission > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material ParticleMat(string name)
        {
            var mat = LoadOrCreateMat(name, "Universal Render Pipeline/Particles/Unlit");
            mat.SetColor("_BaseColor", new Color(3f, 3f, 3f, 1f)); // HDR so sparks feed the bloom
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material LoadOrCreateMat(string name, string shader)
        {
            string path = $"{Root}/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(mat, path);
            }
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

        #endregion
    }
}
