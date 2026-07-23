using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace AtlyssAttackSounds
{
    public enum SoundCategory
    {
        Fast,
        Medium,
        Slow
    }

    public class JiggleController : MonoBehaviour
    {
        private Transform[] bones;
        private readonly Dictionary<Transform, Vector3> originalScales = new Dictionary<Transform, Vector3>();
        private float duration = 0.5f;
        private float elapsed;
        private float intensity = 1.5f;

        public void Init(Transform[] assBones, float jiggleIntensity)
        {
            bones = assBones;
            intensity = jiggleIntensity;
            elapsed = 0f;

            originalScales.Clear();
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
            if (elapsed >= duration)
            {
                ResetBones();
                Destroy(this);
                return;
            }

            float progress = elapsed / duration;
            float scaleOffset = Mathf.Sin(progress * Mathf.PI * 4f) * (1f - progress) * (0.25f * intensity);

            foreach (KeyValuePair<Transform, Vector3> kvp in originalScales)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.localScale = kvp.Value + new Vector3(scaleOffset, scaleOffset, scaleOffset);
                }
            }
        }

        private void ResetBones()
        {
            foreach (KeyValuePair<Transform, Vector3> kvp in originalScales)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.localScale = kvp.Value;
                }
            }
        }
    }

    [BepInPlugin(GUID, NAME, VERSION)]
    public class AtlyssAttackSoundsMod : BaseUnityPlugin
    {
        public const string GUID = "scrithor_Atlyss.Attack.Sounds";
        public const string NAME = "AtlyssAttackSounds";
        public const string VERSION = "1.0.0";

        public static bool FORCE_SLOW_TEST_MODE = false;

        public static ManualLogSource logger;
        public static AtlyssAttackSoundsMod Instance { get; private set; }

        public static ConfigEntry<float> volumeFastConfig;
        public static ConfigEntry<float> volumeMediumConfig;
        public static ConfigEntry<float> volumeSlowConfig;
        public static ConfigEntry<float> jiggleIntensityConfig;
        public static ConfigEntry<float> particleSizeConfig;
        public static ConfigEntry<string> particleStartColorsConfig;

        private static readonly Dictionary<SoundCategory, List<AudioClip>> categoryClips = new Dictionary<SoundCategory, List<AudioClip>>()
        {
            { SoundCategory.Fast, new List<AudioClip>() },
            { SoundCategory.Medium, new List<AudioClip>() },
            { SoundCategory.Slow, new List<AudioClip>() }
        };

        private static GameObject particlePrefab;
        private static readonly System.Random random = new System.Random();
        private Harmony harmony;

        private static float nextAllowedTime;
        private const float COOLDOWN_BUFFER = 0.2f;

        private void Awake()
        {
            Instance = this;
            logger = Logger;

            volumeFastConfig = Config.Bind("Audio Volumes", "Volume_Fast", 1.0f, new ConfigDescription("Fast audio volume.", new AcceptableValueRange<float>(0.0f, 1.0f)));
            volumeMediumConfig = Config.Bind("Audio Volumes", "Volume_Medium", 0.25f, new ConfigDescription("Medium audio volume.", new AcceptableValueRange<float>(0.0f, 1.0f)));
            volumeSlowConfig = Config.Bind("Audio Volumes", "Volume_Slow", 0.3f, new ConfigDescription("Slow audio volume.", new AcceptableValueRange<float>(0.0f, 1.0f)));

            jiggleIntensityConfig = Config.Bind("Effects", "JiggleIntensity", 1.5f, new ConfigDescription("Intensity of physical bone deformation.", new AcceptableValueRange<float>(0.0f, 5.0f)));
            particleSizeConfig = Config.Bind("Effects", "ParticleSize", 0.2f, new ConfigDescription("Particle size distribution.", new AcceptableValueRange<float>(0.01f, 2.0f)));
            particleStartColorsConfig = Config.Bind("Effects", "ParticleStartColors", "CFFF4E, 77F131, 349300", "Initial colors in Hexadecimal.");

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

            logger.LogInfo($"{NAME} v{VERSION} initialized successfully.");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
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
            string[] files = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories);

            foreach (List<AudioClip> list in categoryClips.Values)
            {
                list.Clear();
            }

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
                    if (Time.time < nextAllowedTime) return;

                    Player player = __instance.GetComponent<Player>();
                    if (player == null || player != Player._mainPlayer) return;

                    float appliedDelay = TriggerSoundEffect(player);
                    if (appliedDelay > 0f)
                    {
                        nextAllowedTime = Time.time + appliedDelay;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError($"[ERRO HOOK]: {ex}");
                }
            }
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

            AudioSource source = player.gameObject.GetComponent<AudioSource>();
            if (source == null)
            {
                source = player.gameObject.AddComponent<AudioSource>();
            }

            source.spatialBlend = 0.0f;
            float targetVolume = GetVolumeForCategory(targetCategory);
            source.PlayOneShot(selectedClip, targetVolume);

            if (targetCategory == SoundCategory.Slow)
            {
                TriggerSlowEffects(player);
            }

            return selectedClip.length + COOLDOWN_BUFFER;
        }

        private static void TriggerSlowEffects(Player player)
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
                particleObj.SetActive(true);

                ParticleSystem ps = particleObj.GetComponentInChildren<ParticleSystem>(true);
                if (ps != null)
                {
                    ParticleSystem.MainModule main = ps.main;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    
                    main.duration = 2.0f;
                    main.startLifetime = 2.0f;

                    Color color = ParseColor(particleStartColorsConfig.Value.Split(',')[0], Color.green);
                    main.startColor = new ParticleSystem.MinMaxGradient(color);

                    ps.Play();
                    ps.Emit(30);
                }

                Destroy(particleObj, 2.2f);
            }

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

        private static Color ParseColor(string hex, Color defaultColor)
        {
            hex = hex.Trim().Replace("#", "");
            return ColorUtility.TryParseHtmlString("#" + hex, out Color parsedColor) ? parsedColor : defaultColor;
        }

        private static Transform[] FindAssBones(Transform root)
        {
            string[] exactBoneNames = new string[] 
            { 
                "assbase.l", "assbase.r", 
                "butt.l", "butt.r", 
                "butt_l", "butt_r", 
                "glute.l", "glute.r", 
                "cheek.l", "cheek.r", 
                "ass.l", "ass.r",
                "b_ass_L", "b_ass_R",
                "b_butt_L", "b_butt_R"
            };

            string[] forbiddenKeywords = new string[] 
            { 
                "root", "master", "pelvis", "hip", "body", 
                "chassis", "armature", "player", "spine", "torso", "thigh" 
            };

            List<Transform> foundBones = new List<Transform>();
            Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);

            foreach (Transform t in allTransforms)
            {
                if (t == root) continue;

                string lowerName = t.name.ToLower();

                if (forbiddenKeywords.Any(keyword => lowerName.Contains(keyword))) continue;

                foreach (string target in exactBoneNames)
                {
                    if (lowerName.Equals(target, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!foundBones.Contains(t))
                        {
                            foundBones.Add(t);
                        }
                    }
                }
            }

            return foundBones.ToArray();
        }

        private static SoundCategory SelectCategoryByRarity()
        {
            double roll = random.NextDouble();

            if (roll < 0.84) return SoundCategory.Fast;   // 84% de chance
            if (roll < 0.98) return SoundCategory.Medium; // 14% de chance

            return SoundCategory.Slow;                   // 2% de chance
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
    }
}