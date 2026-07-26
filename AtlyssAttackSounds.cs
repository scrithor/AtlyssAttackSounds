using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace AtlyssAttackSounds
{
    #region Sound Categories

    public enum SoundCategory
    {
        Fast,
        Medium,
        Slow
    }

    #endregion

    #region Jiggle Controller

    public class JiggleController : MonoBehaviour
    {
        private const float DURATION = 0.5f;

        private Transform[] bones;
        private Dictionary<Transform, Vector3> originalScales;
        private float elapsed;
        private float intensity;
        private Vector3 currentScaleOffset;

        public void Init(Transform[] assBones, float jiggleIntensity)
        {
            if (bones != null && originalScales != null)
            {
                ResetBones();
            }

            bones = assBones;
            intensity = jiggleIntensity;
            elapsed = 0f;

            originalScales = new Dictionary<Transform, Vector3>(bones.Length);
            foreach (Transform b in bones)
            {
                if (b != null && !originalScales.ContainsKey(b))
                {
                    originalScales[b] = b.localScale;
                }
            }
        }

        private void LateUpdate()
        {
            if (bones == null || bones.Length == 0) return;

            elapsed += Time.deltaTime;
            if (elapsed >= DURATION)
            {
                ResetBones();
                Destroy(this);
                return;
            }

            float progress = elapsed / DURATION;
            float scaleOffset = Mathf.Sin(progress * Mathf.PI * 4f) * (1f - progress) * (0.25f * intensity);

            currentScaleOffset.x = scaleOffset;
            currentScaleOffset.y = scaleOffset;
            currentScaleOffset.z = scaleOffset;

            foreach (KeyValuePair<Transform, Vector3> kvp in originalScales)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.localScale = kvp.Value + currentScaleOffset;
                }
            }
        }

        public void ResetBones()
        {
            if (originalScales == null) return;

            foreach (KeyValuePair<Transform, Vector3> kvp in originalScales)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.localScale = kvp.Value;
                }
            }
        }

        private void OnDestroy()
        {
            ResetBones();
        }
    }

    public class AttackAudioPool : MonoBehaviour
    {
        private const int DEFAULT_POOL_SIZE = 6;

        private readonly List<AudioSource> sources = new List<AudioSource>();
        private int nextIndex;

        public AudioSource GetSource()
        {
            EnsureInitialized();

            foreach (AudioSource source in sources)
            {
                if (source != null && !source.isPlaying)
                {
                    return source;
                }
            }

            AudioSource fallback = sources[nextIndex % sources.Count];
            nextIndex = (nextIndex + 1) % sources.Count;
            return fallback;
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (sources.Count > 0) return;

            for (int i = 0; i < DEFAULT_POOL_SIZE; i++)
            {
                GameObject audioObject = new GameObject($"AttackSoundSource_{i + 1}");
                audioObject.transform.SetParent(transform, false);

                AudioSource source = audioObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0.0f;
                source.dopplerLevel = 0f;
                source.rolloffMode = AudioRolloffMode.Linear;

                sources.Add(source);
            }
        }
    }

    #endregion

    #region Main Plugin Class

    [BepInPlugin(GUID, NAME, VERSION)]
    public class AtlyssAttackSoundsMod : BaseUnityPlugin
    {
        public const string GUID = "scrithor_Atlyss.Attack.Sounds";
        public const string NAME = "AtlyssAttackSounds";
        public const string VERSION = "1.1.1";

        public const float COOLDOWN_BUFFER = 0.2f;
        private const float MIN_SOUND_INTERVAL = 0.03f;

        public static bool FORCE_SLOW_TEST_MODE = false;

        public static ManualLogSource logger;
        public static AtlyssAttackSoundsMod Instance { get; private set; }

        #region Config Entries

        public static ConfigEntry<float> volumeFastConfig;
        public static ConfigEntry<float> volumeMediumConfig;
        public static ConfigEntry<float> volumeSlowConfig;

        public static ConfigEntry<float> chanceFastConfig;
        public static ConfigEntry<float> chanceMediumConfig;
        public static ConfigEntry<float> chanceSlowConfig;

        public static ConfigEntry<float> jiggleIntensityConfig;
        public static ConfigEntry<float> particleSizeConfig;
        public static ConfigEntry<string> particleStartColorsConfig;

        private static ConfigEntry<KeyboardShortcut> toggleMenuKeyConfig;

        // Delays individuais por categoria de arma (Funciona!)
        public static ConfigEntry<float> delayScepterGrounded;
        public static ConfigEntry<float> delayScepterAir;
        public static ConfigEntry<float> delayBowGrounded;
        public static ConfigEntry<float> delayBowAir;
        public static ConfigEntry<float> delayGreatbladeGrounded;
        public static ConfigEntry<float> delayGreatbladeAir;
        public static ConfigEntry<float> delayBladeGrounded;
        public static ConfigEntry<float> delayBladeAir;
        public static ConfigEntry<float> delayPolearmGrounded;
        public static ConfigEntry<float> delayPolearmAir;
        public static ConfigEntry<float> delayBellGrounded;
        public static ConfigEntry<float> delayBellAir;
        public static ConfigEntry<float> delayKatarGrounded;
        public static ConfigEntry<float> delayKatarAir;
        public static ConfigEntry<float> delayDefaultGrounded;
        public static ConfigEntry<float> delayDefaultAir;

        #endregion

        #region Fields

        private static readonly Dictionary<SoundCategory, List<AudioClip>> categoryClips = new Dictionary<SoundCategory, List<AudioClip>>()
        {
            { SoundCategory.Fast, new List<AudioClip>() },
            { SoundCategory.Medium, new List<AudioClip>() },
            { SoundCategory.Slow, new List<AudioClip>() }
        };

        private static readonly string[] EXACT_BONE_NAMES = new string[]
        {
            "assbase.l", "assbase.r", "butt.l", "butt.r",
            "butt_l", "butt_r", "glute.l", "glute.r",
            "cheek.l", "cheek.r", "ass.l", "ass.r",
            "b_ass_L", "b_ass_R", "b_butt_L", "b_butt_R"
        };

        private static readonly string[] FORBIDDEN_BONE_KEYWORDS = new string[]
        {
            "root", "master", "pelvis", "hip", "body",
            "chassis", "armature", "player", "spine", "torso", "thigh"
        };

        private static GameObject particlePrefab;
        private static readonly System.Random random = new System.Random();

        private static readonly FieldInfo playerCombatField = AccessTools.Field(typeof(Player), "_pCombat");
        private static readonly FieldInfo playerVisualField = AccessTools.Field(typeof(Player), "_pVisual");
        private static readonly FieldInfo playerActionField = AccessTools.Field(typeof(Player), "_currentPlayerAction");
        private static readonly FieldInfo currentSwingStateField = AccessTools.Field(typeof(PlayerCombat), "_currentSwingState");
        private static readonly FieldInfo visualAnimatorField = AccessTools.Field(typeof(PlayerVisual), "_visualAnimator");
        private static readonly FieldInfo equippedWeaponField = AccessTools.Field(typeof(PlayerCombat), "_equippedWeapon");

        private static float nextAllowedTime;
        private static float lastAttackTriggerTime;
        private static float currentAttackCooldown;

        private Harmony harmony;
        private bool wasToggleKeyPressed;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            Instance = this;
            logger = Logger;

            InitConfiguration();

            string pluginDir = Path.GetDirectoryName(Info.Location);
            string soundsDir = Path.Combine(pluginDir, "sounds");
            string assetsPath = Path.Combine(pluginDir, "Assets", "atlyss");

            LoadAssetBundle(assetsPath);

            if (Directory.Exists(soundsDir))
            {
                StartCoroutine(LoadAudioFiles(soundsDir));
            }

            harmony = new Harmony(GUID);
            harmony.PatchAll(typeof(AtlyssAttackSoundsMod).Assembly);

            logger.LogInfo($"{NAME} v{VERSION} initialized.");
        }

        private void Start()
        {
            SettingsUI.Initialize(
                volumeFastConfig, volumeMediumConfig, volumeSlowConfig,
                chanceFastConfig, chanceMediumConfig, chanceSlowConfig,
                jiggleIntensityConfig, particleSizeConfig
            );
        }

        private void Update()
        {
            HandleSettingsToggle();
        }

        private void OnDestroy()
        {
            UnloadAudioClips();
            harmony?.UnpatchSelf();
        }

        #endregion

        #region Input Handling

        private void HandleSettingsToggle()
        {
            KeyboardShortcut toggleKey = toggleMenuKeyConfig?.Value ?? new KeyboardShortcut(KeyCode.F7);
            bool keyDown = toggleKey.IsDown();

            if (keyDown && !wasToggleKeyPressed)
            {
                wasToggleKeyPressed = true;
                SettingsUI.ToggleVisible();
            }
            else if (!keyDown)
            {
                wasToggleKeyPressed = false;
            }
        }

        #endregion

        private void InitConfiguration()
        {
            volumeFastConfig = Config.Bind("Audio Volumes", "Volume_Fast", 1.0f, new ConfigDescription("Fast audio volume.", new AcceptableValueRange<float>(0.0f, 1.0f)));
            volumeMediumConfig = Config.Bind("Audio Volumes", "Volume_Medium", 0.85f, new ConfigDescription("Medium audio volume.", new AcceptableValueRange<float>(0.0f, 1.0f)));
            volumeSlowConfig = Config.Bind("Audio Volumes", "Volume_Slow", 0.3f, new ConfigDescription("Slow audio volume.", new AcceptableValueRange<float>(0.0f, 1.0f)));

            chanceFastConfig = Config.Bind("Proc Chances", "Chance_Fast", 84.0f, new ConfigDescription("Relative weight for Fast sounds.", new AcceptableValueRange<float>(0.0f, 100.0f)));
            chanceMediumConfig = Config.Bind("Proc Chances", "Chance_Medium", 12.0f, new ConfigDescription("Relative weight for Medium sounds.", new AcceptableValueRange<float>(0.0f, 100.0f)));
            chanceSlowConfig = Config.Bind("Proc Chances", "Chance_Slow", 4.0f, new ConfigDescription("Relative weight for Slow sounds.", new AcceptableValueRange<float>(0.0f, 100.0f)));

            jiggleIntensityConfig = Config.Bind("Effects", "JiggleIntensity", 1.5f, new ConfigDescription("Intensity of physical bone deformation.", new AcceptableValueRange<float>(0.0f, 5.0f)));
            particleSizeConfig = Config.Bind("Effects", "ParticleSize", 0.2f, new ConfigDescription("Particle size distribution.", new AcceptableValueRange<float>(0.01f, 2.0f)));
            particleStartColorsConfig = Config.Bind("Effects", "ParticleStartColors", "CFFF4E, 77F131, 349300", "Initial colors in Hexadecimal.");

            toggleMenuKeyConfig = Config.Bind("Settings Menu", "ToggleMenuKey", new KeyboardShortcut(KeyCode.F7), "Key to open/close the settings menu.");

            // Inicialização dos Delays Customizáveis por Arma
            delayScepterGrounded = Config.Bind("Weapon Delays", "Scepter_Grounded", 0.533f, "Delay para Scepter no chão");
            delayScepterAir = Config.Bind("Weapon Delays", "Scepter_Air", 0.533f, "Delay para Scepter no ar");

            delayBowGrounded = Config.Bind("Weapon Delays", "Bow_Grounded", 0.366f, "Delay para Bow no chão");
            delayBowAir = Config.Bind("Weapon Delays", "Bow_Air", 0.366f, "Delay para Bow no ar");

            delayGreatbladeGrounded = Config.Bind("Weapon Delays", "Greatblade_Grounded", 0.780f, "Delay para Greatblade no chão");
            delayGreatbladeAir = Config.Bind("Weapon Delays", "Greatblade_Air", 0.993f, "Delay para Greatblade no ar");

            delayBladeGrounded = Config.Bind("Weapon Delays", "Blade_Grounded", 0.460f, "Delay para Blade no chão");
            delayBladeAir = Config.Bind("Weapon Delays", "Blade_Air", 0.980f, "Delay para Blade no ar");

            delayPolearmGrounded = Config.Bind("Weapon Delays", "Polearm_Grounded", 0.490f, "Delay para Polearm no chão");
            delayPolearmAir = Config.Bind("Weapon Delays", "Polearm_Air", 0.850f, "Delay para Polearm no ar");

            delayBellGrounded = Config.Bind("Weapon Delays", "Bell_Grounded", 0.980f, "Delay para Bell no chão");
            delayBellAir = Config.Bind("Weapon Delays", "Bell_Air", 0.633f, "Delay para Bell no ar");

            delayKatarGrounded = Config.Bind("Weapon Delays", "Katar_Grounded", 0.266f, "Delay para Katar no chão");
            delayKatarAir = Config.Bind("Weapon Delays", "Katar_Air", 0.720f, "Delay para Katar no ar");

            delayDefaultGrounded = Config.Bind("Weapon Delays", "Default_Grounded", 0.400f, "Delay padrão para outras armas no chão");
            delayDefaultAir = Config.Bind("Weapon Delays", "Default_Air", 0.500f, "Delay padrão para outras armas no ar");
        }

        private void LoadAssetBundle(string bundlePath)
        {
            if (!File.Exists(bundlePath))
            {
                logger.LogError($"[ERRO ASSETBUNDLE] File missing in: {bundlePath}");
                return;
            }

            AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
            if (bundle == null)
            {
                logger.LogError("[ERRO ASSETBUNDLE] Unable to load the asset package.");
                return;
            }

            GameObject[] loadedAssets = bundle.LoadAllAssets<GameObject>();
            foreach (GameObject asset in loadedAssets)
            {
                if (asset.GetComponentInChildren<ParticleSystem>(true) != null)
                {
                    particlePrefab = asset;
                    break;
                }
            }

            if (particlePrefab == null && loadedAssets.Length > 0)
            {
                particlePrefab = loadedAssets[0];
            }

            if (particlePrefab != null)
            {
                logger.LogInfo($"[ASSETBUNDLE SUCESSO] Prefab de partícula identificado: {particlePrefab.name}");
            }
        }

        private IEnumerator LoadAudioFiles(string directoryPath)
        {
            UnloadAudioClips();

            string[] files = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories);

            foreach (string filePath in files)
            {
                string extension = Path.GetExtension(filePath).ToLower();
                AudioType audioType = extension switch
                {
                    ".wav" => AudioType.WAV,
                    ".ogg" => AudioType.OGGVORBIS,
                    ".mp3" => AudioType.MPEG,
                    _ => AudioType.UNKNOWN
                };

                if (audioType == AudioType.UNKNOWN) continue;

                string fileUri = new Uri(filePath).AbsoluteUri;

                using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(fileUri, audioType))
                {
                    yield return www.SendWebRequest();

                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                        clip.name = Path.GetFileNameWithoutExtension(filePath);

                        SoundCategory category = DetermineCategory(filePath);
                        categoryClips[category].Add(clip);
                    }
                }
            }
        }

        private void UnloadAudioClips()
        {
            foreach (List<AudioClip> list in categoryClips.Values)
            {
                foreach (AudioClip clip in list)
                {
                    if (clip != null)
                    {
                        Destroy(clip);
                    }
                }
                list.Clear();
            }
        }

        private static SoundCategory DetermineCategory(string filePath)
        {
            string parentDir = Path.GetFileName(Path.GetDirectoryName(filePath))?.ToLower() ?? "";

            if (parentDir.Contains("medium")) return SoundCategory.Medium;
            if (parentDir.Contains("slow")) return SoundCategory.Slow;

            return SoundCategory.Fast;
        }

        [HarmonyPatch(typeof(PlayerCombat), "Init_Attack")]
        public static class AttackDetector
        {
            [HarmonyPostfix]
            private static void Postfix(PlayerCombat __instance)
            {
                try
                {
                    Player player = __instance.GetComponent<Player>();
                    if (player == null || player != Player._mainPlayer) return;

                    if (Instance != null)
                    {
                        Instance.StartCoroutine(ProcessAttackWithCustomDelay(player));
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError($"[ERRO HOOK]: {ex}");
                }
            }
        }

        private static IEnumerator ProcessAttackWithCustomDelay(Player player)
        {
            if (player == null) yield break;

            if (Time.time - lastAttackTriggerTime < currentAttackCooldown)
            {
                yield break;
            }

            bool isAirAttack = !GetIsGrounded(player);
            string weaponName = GetEquippedWeaponName(player);
            float delay = GetAnimationDuration(weaponName, isAirAttack);

            lastAttackTriggerTime = Time.time;
            currentAttackCooldown = delay;

            yield return new WaitForSeconds(delay);

            if (player != null && player == Player._mainPlayer)
            {
                TryTriggerResolvedAttackEffect(player);
            }
        }

        private static float GetAnimationDuration(string weaponName, bool isAirAttack)
        {
            string weaponKey = weaponName.ToLower();

            if (weaponKey.Contains("scepter"))
            {
                return isAirAttack ? delayScepterAir.Value : delayScepterGrounded.Value;
            }
            if (weaponKey.Contains("bow"))
            {
                return isAirAttack ? delayBowAir.Value : delayBowGrounded.Value;
            }
            if (weaponKey.Contains("greatblade"))
            {
                return isAirAttack ? delayGreatbladeAir.Value : delayGreatbladeGrounded.Value;
            }
            if (weaponKey.Contains("blade"))
            {
                return isAirAttack ? delayBladeAir.Value : delayBladeGrounded.Value;
            }
            if (weaponKey.Contains("polearm"))
            {
                return isAirAttack ? delayPolearmAir.Value : delayPolearmGrounded.Value;
            }
            if (weaponKey.Contains("bell"))
            {
                return isAirAttack ? delayBellAir.Value : delayBellGrounded.Value;
            }
            if (weaponKey.Contains("katar"))
            {
                return isAirAttack ? delayKatarAir.Value : delayKatarGrounded.Value;
            }

            return isAirAttack ? delayDefaultAir.Value : delayDefaultGrounded.Value;
        }

        private static bool GetIsGrounded(Player player)
        {
            if (player == null) return true;

            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                return controller.isGrounded;
            }

            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                return Mathf.Abs(rb.velocity.y) < 0.01f;
            }

            return true;
        }

        private static string GetEquippedWeaponName(Player player)
        {
            if (player == null || playerCombatField == null || equippedWeaponField == null) return string.Empty;

            PlayerCombat playerCombat = playerCombatField.GetValue(player) as PlayerCombat;
            if (playerCombat == null) return string.Empty;

            object weaponObj = equippedWeaponField.GetValue(playerCombat);
            if (weaponObj != null)
            {
                return weaponObj.ToString();
            }

            return string.Empty;
        }

        private static bool TryTriggerResolvedAttackEffect(Player player)
        {
            if (player == null || player != Player._mainPlayer) return false;
            if (Time.time < nextAllowedTime) return false;

            float appliedDelay = TriggerSoundEffect(player);
            if (appliedDelay <= 0f) return false;

            nextAllowedTime = Time.time + MIN_SOUND_INTERVAL;
            return true;
        }

        private static float TriggerSoundEffect(Player player)
        {
            int totalClips = categoryClips.Values.Sum(l => l.Count);
            if (totalClips == 0) return 0f;

            SoundCategory targetCategory = FORCE_SLOW_TEST_MODE ? SoundCategory.Slow : SelectCategoryByRarity();
            List<AudioClip> pool = categoryClips[targetCategory];

            if (pool.Count == 0)
            {
                var availableCategories = categoryClips.Where(kv => kv.Value.Count > 0).ToList();
                if (availableCategories.Count == 0) return 0f;
                pool = availableCategories[random.Next(availableCategories.Count)].Value;
                targetCategory = categoryClips.First(kv => kv.Value == pool).Key;
            }

            AudioClip selectedClip = pool[random.Next(pool.Count)];

            AttackAudioPool audioPool = GetAudioPool(player);
            if (audioPool == null) return 0f;

            AudioSource source = audioPool.GetSource();
            if (source == null) return 0f;

            float targetVolume = GetVolumeForCategory(targetCategory);
            source.PlayOneShot(selectedClip, targetVolume);

            TriggerJiggleEffect(player);

            if (targetCategory == SoundCategory.Slow)
            {
                TriggerParticleEffect(player);
            }

            return selectedClip.length;
        }

        private static AttackAudioPool GetAudioPool(Player player)
        {
            if (player == null) return null;

            AttackAudioPool existingPool = player.GetComponentInChildren<AttackAudioPool>(true);
            if (existingPool != null) return existingPool;

            GameObject poolObject = new GameObject("AttackSoundsAudioPool");
            poolObject.transform.SetParent(player.transform, false);
            poolObject.hideFlags = HideFlags.HideAndDontSave;

            return poolObject.AddComponent<AttackAudioPool>();
        }

        private static void TriggerJiggleEffect(Player player)
        {
            Transform[] assBones = FindAssBones(player.transform);

            if (assBones.Length > 0)
            {
                JiggleController jiggle = player.gameObject.GetComponent<JiggleController>();
                if (jiggle == null)
                {
                    jiggle = player.gameObject.AddComponent<JiggleController>();
                }
                jiggle.Init(assBones, jiggleIntensityConfig.Value);
            }
        }

        private static void TriggerParticleEffect(Player player)
        {
            Transform[] assBones = FindAssBones(player.transform);

            if (particlePrefab != null)
            {
                Vector3 spawnPosition = player.transform.position + (player.transform.up * 0.4f);
                if (assBones.Length > 0)
                {
                    spawnPosition = assBones[0].position;
                }

                GameObject particleObj = Instantiate(particlePrefab, spawnPosition, Quaternion.identity);
                particleObj.transform.localScale = Vector3.one * particleSizeConfig.Value;

                particleObj.transform.SetParent(player.transform, true);
                particleObj.SetActive(true);

                ParticleSystem ps = particleObj.GetComponentInChildren<ParticleSystem>(true);
                if (ps != null)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                    ParticleSystem.MainModule main = ps.main;
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
                    main.duration = 1.7f;
                    main.startLifetime = 1.7f;
                    main.startSpeed = 1.0f;
                    main.gravityModifier = 0f;
                    main.loop = false;
                    main.playOnAwake = false;

                    Color color = ParseColor(particleStartColorsConfig.Value.Split(',')[0], Color.green);
                    main.startColor = new ParticleSystem.MinMaxGradient(color);

                    ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
                    col.enabled = true;

                    Gradient grad = new Gradient();
                    float fadeStart = 0.9f / 1.7f;
                    grad.SetKeys(
                        new GradientColorKey[] {
                            new GradientColorKey(Color.white, 0f),
                            new GradientColorKey(Color.white, 1f)
                        },
                        new GradientAlphaKey[] {
                            new GradientAlphaKey(1f, 0f),
                            new GradientAlphaKey(1f, fadeStart),
                            new GradientAlphaKey(0f, 1f)
                        }
                    );
                    col.color = new ParticleSystem.MinMaxGradient(grad);

                    ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
                    sol.enabled = true;
                    AnimationCurve sizeCurve = new AnimationCurve();
                    sizeCurve.AddKey(0f, 1f);
                    sizeCurve.AddKey(fadeStart, 1f);
                    sizeCurve.AddKey(1f, 0.2f);
                    sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

                    ps.Clear(true);
                    ps.Play();
                    ps.Emit(30);

                    logger.LogInfo($"[PARTICLE] Emitted with fade: dur={main.duration}, lifetime={main.startLifetime}, fadeStart={fadeStart}");
                }

                Destroy(particleObj, 1.8f);
            }
        }

        private static Color ParseColor(string hex, Color defaultColor)
        {
            hex = hex.Trim().Replace("#", "");
            return ColorUtility.TryParseHtmlString("#" + hex, out Color parsedColor) ? parsedColor : defaultColor;
        }

        private static Transform[] FindAssBones(Transform root)
        {
            List<Transform> foundBones = new List<Transform>();
            Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);

            foreach (Transform t in allTransforms)
            {
                if (t == root) continue;

                string lowerName = t.name.ToLower();

                if (FORBIDDEN_BONE_KEYWORDS.Any(keyword => lowerName.Contains(keyword))) continue;

                foreach (string target in EXACT_BONE_NAMES)
                {
                    if (lowerName.Equals(target, StringComparison.OrdinalIgnoreCase) && !foundBones.Contains(t))
                    {
                        foundBones.Add(t);
                    }
                }
            }

            return foundBones.ToArray();
        }

        private static SoundCategory SelectCategoryByRarity()
        {
            float weightFast = Mathf.Max(0f, chanceFastConfig.Value);
            float weightMedium = Mathf.Max(0f, chanceMediumConfig.Value);
            float weightSlow = Mathf.Max(0f, chanceSlowConfig.Value);

            float totalWeight = weightFast + weightMedium + weightSlow;

            if (totalWeight <= 0f) return SoundCategory.Fast;

            double roll = random.NextDouble() * totalWeight;

            if (roll < weightFast) return SoundCategory.Fast;
            if (roll < weightFast + weightMedium) return SoundCategory.Medium;

            return SoundCategory.Slow;
        }

        private static float GetVolumeForCategory(SoundCategory category)
        {
            return category switch
            {
                SoundCategory.Medium => Mathf.Clamp(volumeMediumConfig.Value, 0.0f, 1.0f),
                SoundCategory.Slow => Mathf.Clamp(volumeSlowConfig.Value, 0.0f, 1.0f),
                _ => Mathf.Clamp(volumeFastConfig.Value, 0.0f, 1.0f),
            };
        }

        #endregion
    }
}