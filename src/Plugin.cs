using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace REPOAllSeeingGod
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInProcess("REPO.exe")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "cn.codex.REPO.AllSeeingGod";
        public const string Name = "All Seeing God / 全知无敌";
        public const string Version = "1.0.4";

        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        internal static Plugin Instance;
        private Harmony harmony;

        private ConfigEntry<bool> godMode;
        private ConfigEntry<int> maxHealth;
        private ConfigEntry<float> staminaLimit;
        private ConfigEntry<bool> alwaysShowMap;
        private ConfigEntry<bool> showEnemies;
        private ConfigEntry<bool> showValuables;
        private ConfigEntry<float> mapWidth;
        private ConfigEntry<float> mapHeight;
        private ConfigEntry<float> mapZoom;
        private ConfigEntry<float> mapOpacity;
        private ConfigEntry<KeyCode> toggleKey;

        private Type playerHealthType;
        private Type playerControllerType;
        private Type enemyRigidbodyType;
        private Type valuableObjectType;
        private Type mapType;
        private Type mapCustomType;

        private Camera mapCamera;
        private RenderTexture mapTexture;
        private object lastMapInstance;
        private object cachedLocalHealth;
        private object cachedController;
        private int initializedControllerId;
        private float nextScan;
        private bool mapVisible = true;
        private string healthStatus = "等待 PlayerHealth...";
        private float nextMissingHealthLog;
        private Sprite enemySprite;
        private readonly HashSet<int> registeredValuables = new HashSet<int>();
        private readonly HashSet<int> diagnosedHealthObjects = new HashSet<int>();

        private void Awake()
        {
            Instance = this;
            // R.E.P.O. replaces scene roots while moving between menu, lobby and level.
            // Detach and persist exactly like current v0.4 Minimap/GodMode plugins.
            gameObject.transform.parent = null;
            gameObject.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(gameObject);
            BindConfig();
            ResolveTypes();
            harmony = new Harmony(Guid);
            PatchPlayerHealth();
            enemySprite = CreateCircleSprite(16);
            Logger.LogInfo(Name + " " + Version + " loaded; persistent runner enabled");
        }

        private void BindConfig()
        {
            godMode = Config.Bind("01-人物", "无敌", true, "阻止本地玩家受到伤害，并持续恢复为满血。");
            maxHealth = Config.Bind("01-人物", "生命上限", 500, new ConfigDescription("本地玩家生命上限。", new AcceptableValueRange<int>(1, 100000)));
            staminaLimit = Config.Bind("01-人物", "体力上限", 100f, new ConfigDescription("默认体力约为 40；建议 80-200。", new AcceptableValueRange<float>(1f, 10000f)));
            alwaysShowMap = Config.Bind("02-地图", "常显地图", true, "无需按 Tab，在屏幕上常驻显示原生地图。");
            showEnemies = Config.Bind("02-地图", "显示怪物", true, "在地图上用红点标记怪物。");
            showValuables = Config.Bind("02-地图", "显示宝物", true, "在地图上显示所有宝物。");
            mapWidth = Config.Bind("03-外观", "地图宽度", 360f, new ConfigDescription("像素。", new AcceptableValueRange<float>(160f, 900f)));
            mapHeight = Config.Bind("03-外观", "地图高度", 300f, new ConfigDescription("像素。", new AcceptableValueRange<float>(120f, 700f)));
            mapZoom = Config.Bind("03-外观", "地图缩放", 2.25f, new ConfigDescription("数值越大，显示范围越广。", new AcceptableValueRange<float>(0.5f, 12f)));
            mapOpacity = Config.Bind("03-外观", "地图透明度", 0.9f, new ConfigDescription("0.1-1。", new AcceptableValueRange<float>(0.1f, 1f)));
            toggleKey = Config.Bind("03-外观", "地图开关键", KeyCode.F8, "临时显示/隐藏常驻地图。");
        }

        private void ResolveTypes()
        {
            playerHealthType = GameType("PlayerHealth");
            playerControllerType = GameType("PlayerController");
            enemyRigidbodyType = GameType("EnemyRigidbody");
            valuableObjectType = GameType("ValuableObject");
            mapType = GameType("Map");
            mapCustomType = GameType("MapCustom");
        }

        private static Type GameType(string name)
        {
            return Type.GetType(name + ", Assembly-CSharp", false);
        }

        private void PatchPlayerHealth()
        {
            if (playerHealthType == null)
            {
                Logger.LogWarning("PlayerHealth type not found; health protection will retry by refill only.");
                return;
            }

            MethodInfo prefix = AccessTools.Method(typeof(Plugin), nameof(HurtPrefix));
            MethodInfo postfix = AccessTools.Method(typeof(Plugin), nameof(HealthPostfix));
            int damagePatchCount = 0;
            foreach (MethodInfo method in playerHealthType.GetMethods(AnyInstance))
            {
                if (method.Name == "Hurt" || method.Name == "HurtOther")
                {
                    harmony.Patch(method, new HarmonyMethod(prefix));
                    damagePatchCount++;
                }
            }

            MethodInfo start = AccessTools.Method(playerHealthType, "Start");
            MethodInfo update = AccessTools.Method(playerHealthType, "Update");
            if (start != null) harmony.Patch(start, postfix: new HarmonyMethod(postfix));
            if (update != null) harmony.Patch(update, postfix: new HarmonyMethod(postfix));
            Logger.LogInfo("PlayerHealth patches installed: damage=" + damagePatchCount +
                           ", start=" + (start != null) + ", update=" + (update != null));
        }

        private static bool HurtPrefix(object __instance)
        {
            Plugin plugin = Instance;
            if (plugin == null || !plugin.godMode.Value)
                return true;
            // Under CrossOver/Wine, v0.4 can report IsMine=false for local PlayerHealth.
            // Current working v0.4 GodMode implementations patch each live PlayerHealth.
            return false;
        }

        private static void HealthPostfix(object __instance)
        {
            Plugin plugin = Instance;
            if (plugin != null)
                plugin.ApplyHealthTo(__instance);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey.Value))
                mapVisible = !mapVisible;

            try
            {
                ApplyPlayerStats();
            }
            catch (Exception ex)
            {
                Logger.LogError("Player stat update failed: " + ex);
            }

            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 0.35f;
                try { RefreshMap(); }
                catch (Exception ex) { Logger.LogError("Map update failed: " + ex); }
            }
        }

        private void ApplyPlayerStats()
        {
            if (playerHealthType != null && !IsAliveComponent(cachedLocalHealth))
                cachedLocalHealth = FindAnyPlayerHealth();

            if (cachedLocalHealth != null)
                ApplyHealthTo(cachedLocalHealth);
            else if (playerHealthType != null && Time.unscaledTime >= nextMissingHealthLog)
            {
                nextMissingHealthLog = Time.unscaledTime + 5f;
                healthStatus = "未找到 PlayerHealth";
                Logger.LogWarning("No active PlayerHealth found yet. Enter a playable level and check again.");
            }

            if (playerControllerType == null)
                return;

            if (!IsAliveComponent(cachedController))
            {
                cachedController = GetSingleton(playerControllerType, "instance") ?? FindFirst(playerControllerType);
                initializedControllerId = 0;
            }

            Component controller = cachedController as Component;
            if (controller == null)
                return;

            bool foundMax = false;
            string[] maxNames = { "EnergyMax", "energyMax", "maxEnergy", "EnergyStart", "energyStart" };
            foreach (string fieldName in maxNames)
                foundMax |= SetNumeric(cachedController, fieldName, staminaLimit.Value);

            int id = controller.GetInstanceID();
            if (initializedControllerId != id)
            {
                // Older game builds expose only EnergyCurrent. Give the larger reserve once,
                // but do not refill it every frame (this is intentionally not infinite stamina).
                SetNumeric(cachedController, "EnergyCurrent", staminaLimit.Value);
                SetNumeric(cachedController, "energyCurrent", staminaLimit.Value);
                initializedControllerId = id;
                if (!foundMax)
                    Logger.LogInfo("No stamina max field exposed; applied one-time high stamina reserve fallback.");
            }
        }

        private void ApplyHealthTo(object healthObject)
        {
            Component component = healthObject as Component;
            if (component == null) return;

            bool maxSet = SetNumeric(healthObject, "maxHealth", maxHealth.Value);
            bool healthSet = !godMode.Value || SetNumeric(healthObject, "health", maxHealth.Value);
            bool nativeGodSet = SetBoolean(healthObject, "godMode", godMode.Value);

            int id = component.GetInstanceID();
            if (!diagnosedHealthObjects.Add(id)) return;

            object actualHealth = GetMember(healthObject, "health");
            object actualMax = GetMember(healthObject, "maxHealth");
            healthStatus = "HP " + actualHealth + "/" + actualMax +
                           "  God=" + (GetMember(healthObject, "godMode") ?? godMode.Value);
            if (maxSet && healthSet)
            {
                Logger.LogInfo("Health applied to " + component.gameObject.name +
                               ": health=" + actualHealth + "/" + actualMax +
                               ", nativeGodModeField=" + nativeGodSet);
            }
            else
            {
                Logger.LogError("Health fields not compatible. maxHealthFound=" + maxSet +
                                ", healthFound=" + healthSet + ". Available fields: " +
                                String.Join(", ", GetFieldNames(healthObject.GetType())));
            }
        }

        private void RefreshMap()
        {
            object map = GetSingleton(mapType, "Instance") ?? GetSingleton(mapType, "instance");
            if (map == null)
                return;

            if (!ReferenceEquals(lastMapInstance, map))
            {
                lastMapInstance = map;
                registeredValuables.Clear();
                mapCamera = null;
                mapTexture = null;
            }

            if (alwaysShowMap.Value)
                Invoke(map, "ActiveSet", true);

            FindMapCamera();
            if (mapCamera != null)
            {
                mapCamera.orthographicSize = mapZoom.Value;
                if (mapCamera.activeTexture != null)
                    mapTexture = mapCamera.activeTexture;
            }

            if (showValuables.Value)
                RegisterValuables(map);
            if (showEnemies.Value)
                RegisterEnemies();
        }

        private void FindMapCamera()
        {
            if (mapCamera != null)
                return;
            Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
            foreach (Camera candidate in cameras)
            {
                if (candidate != null && candidate.name == "Dirt Finder Map Camera")
                {
                    mapCamera = candidate;
                    break;
                }
            }
        }

        private void RegisterValuables(object map)
        {
            if (valuableObjectType == null)
                return;
            UnityEngine.Object[] valuables = UnityEngine.Object.FindObjectsOfType(valuableObjectType);
            foreach (UnityEngine.Object valuable in valuables)
            {
                if (valuable == null || !registeredValuables.Add(valuable.GetInstanceID()))
                    continue;
                Invoke(map, "AddValuable", valuable);
            }
        }

        private void RegisterEnemies()
        {
            if (enemyRigidbodyType == null || mapCustomType == null)
                return;
            UnityEngine.Object[] enemies = UnityEngine.Object.FindObjectsOfType(enemyRigidbodyType);
            foreach (UnityEngine.Object enemy in enemies)
            {
                Component component = enemy as Component;
                if (component == null || !component.gameObject.activeInHierarchy)
                    continue;

                Component marker = component.GetComponent(mapCustomType);
                if (marker == null)
                    marker = component.gameObject.AddComponent(mapCustomType);
                SetMember(marker, "sprite", enemySprite);
                SetMember(marker, "color", Color.red);
            }
        }

        private void OnGUI()
        {
            GUI.depth = -10000;
            Color previousColor = GUI.color;
            GUI.color = new Color(0.1f, 0.85f, 0.25f, 1f);
            GUI.Box(new Rect(14f, 14f, 330f, 26f), "All Seeing God v" + Version + " | " + healthStatus);
            GUI.color = previousColor;

            if (!alwaysShowMap.Value || !mapVisible || mapTexture == null)
                return;

            float width = mapWidth.Value;
            float height = mapHeight.Value;
            Rect mapRect = new Rect(Screen.width - width - 16f, 32f, width, height);
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, mapOpacity.Value);
            GUI.DrawTexture(mapRect, mapTexture, ScaleMode.StretchToFill, false);
            GUI.color = old;

            GUI.Box(new Rect(mapRect.x, mapRect.y + height + 3f, width, 24f),
                "红色=怪物   黄色/原生图标=宝物   [" + toggleKey.Value + "] 隐藏地图");
        }

        private object FindAnyPlayerHealth()
        {
            object singleton = GetSingleton(playerHealthType, "instance") ?? GetSingleton(playerHealthType, "Instance");
            if (IsAliveComponent(singleton)) return singleton;

            UnityEngine.Object[] all = UnityEngine.Object.FindObjectsOfType(playerHealthType);
            return all.Length > 0 ? all[0] : null;
        }

        private bool IsLocalPlayerHealth(object health)
        {
            if (!IsAliveComponent(health))
                return false;
            object menu = GetMember(health, "isMenuAvatar");
            if (menu is bool && (bool)menu)
                return false;
            object view = GetMember(health, "photonView");
            object isMine = GetMember(view, "IsMine");
            return !(isMine is bool) || (bool)isMine;
        }

        private static bool IsAliveComponent(object value)
        {
            Component component = value as Component;
            return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
        }

        private static object FindFirst(Type type)
        {
            if (type == null) return null;
            UnityEngine.Object[] values = UnityEngine.Object.FindObjectsOfType(type);
            return values.Length > 0 ? values[0] : null;
        }

        private static object GetSingleton(Type type, string name)
        {
            if (type == null) return null;
            FieldInfo field = type.GetField(name, AnyStatic);
            if (field != null) return field.GetValue(null);
            PropertyInfo property = type.GetProperty(name, AnyStatic);
            return property != null ? property.GetValue(null, null) : null;
        }

        private static object GetMember(object target, string name)
        {
            if (target == null) return null;
            Type type = target.GetType();
            FieldInfo field = type.GetField(name, AnyInstance);
            if (field != null) return field.GetValue(target);
            PropertyInfo property = type.GetProperty(name, AnyInstance);
            return property != null ? property.GetValue(target, null) : null;
        }

        private static void SetMember(object target, string name, object value)
        {
            if (target == null) return;
            Type type = target.GetType();
            FieldInfo field = type.GetField(name, AnyInstance);
            if (field != null) { field.SetValue(target, value); return; }
            PropertyInfo property = type.GetProperty(name, AnyInstance);
            if (property != null && property.CanWrite) property.SetValue(target, value, null);
        }

        private static bool SetNumeric(object target, string name, object value)
        {
            if (target == null) return false;
            FieldInfo field = target.GetType().GetField(name, AnyInstance);
            if (field != null)
            {
                field.SetValue(target, Convert.ChangeType(value, field.FieldType));
                return true;
            }
            PropertyInfo property = target.GetType().GetProperty(name, AnyInstance);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, Convert.ChangeType(value, property.PropertyType), null);
                return true;
            }
            return false;
        }

        private static bool SetBoolean(object target, string name, bool value)
        {
            if (target == null) return false;
            FieldInfo field = target.GetType().GetField(name, AnyInstance);
            if (field != null && field.FieldType == typeof(bool))
            {
                field.SetValue(target, value);
                return true;
            }
            PropertyInfo property = target.GetType().GetProperty(name, AnyInstance);
            if (property != null && property.CanWrite && property.PropertyType == typeof(bool))
            {
                property.SetValue(target, value, null);
                return true;
            }
            return false;
        }

        private static string[] GetFieldNames(Type type)
        {
            FieldInfo[] fields = type.GetFields(AnyInstance);
            string[] names = new string[fields.Length];
            for (int i = 0; i < fields.Length; i++) names[i] = fields[i].Name;
            return names;
        }

        private static object Invoke(object target, string name, params object[] args)
        {
            if (target == null) return null;
            MethodInfo method = AccessTools.Method(target.GetType(), name);
            return method != null ? method.Invoke(target, args) : null;
        }

        private static Sprite CreateCircleSprite(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float radius2 = center * center;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    pixels[y * size + x] = dx * dx + dy * dy <= radius2 ? Color.white : Color.clear;
                }
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private void OnDestroy()
        {
            if (harmony != null) harmony.UnpatchSelf();
        }
    }
}
