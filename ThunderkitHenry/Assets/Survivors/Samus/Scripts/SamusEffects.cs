using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace SamusMod.Modules
{
    // Extra explosion, muzzle flash and light effects built from vanilla effects, to fill out Samus's own sparse ones.
    internal static class SamusEffects
    {
        private static GameObject explosion;
        private static GameObject chargedMuzzleFlash;
        private static GameObject vanillaMissileGhost;

        internal static void Init()
        {
            explosion = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniEffect/OmniExplosionVFX");
            chargedMuzzleFlash = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/MuzzleFlashes/MuzzleflashMageFireLarge");
            vanillaMissileGhost = LegacyResourcesAPI.Load<GameObject>("Prefabs/ProjectileGhosts/MissileGhost");
        }

        // Spawns a vanilla explosion for everyone. Scale is the explosion's radius in meters.
        internal static void Burst(Vector3 position, float scale)
        {
            if (!explosion)
            {
                return;
            }

            EffectManager.SpawnEffect(explosion, new EffectData { origin = position, scale = scale }, true);
        }

        internal static void ChargedMuzzleFlash(GameObject body, string muzzleName)
        {
            if (chargedMuzzleFlash)
            {
                EffectManager.SimpleMuzzleFlash(chargedMuzzleFlash, body, muzzleName, false);
            }
        }

        // A short-lived point light that only the local player sees.
        internal static void Flash(Vector3 position, Color color, float intensity, float range, float duration)
        {
            var flashObject = new GameObject("SamusLightFlash");
            flashObject.transform.position = position;

            var light = flashObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;

            var flash = flashObject.AddComponent<SamusLightFlash>();
            flash.light = light;
            flash.startIntensity = intensity;
            flash.duration = duration;
        }

        // Gives a projectile prefab an explosion when it is destroyed.
        internal static void AddImpactBurst(GameObject projectilePrefab, float baseScale, float scalePerTransformScale = 0f)
        {
            if (!projectilePrefab)
            {
                return;
            }

            var burst = projectilePrefab.GetComponent<SamusImpactBurst>() ?? projectilePrefab.AddComponent<SamusImpactBurst>();
            burst.baseScale = baseScale;
            burst.scalePerTransformScale = scalePerTransformScale;
        }

        // Copies the smoke and fire particles from the vanilla missile onto a projectile's ghost so it trails.
        internal static void AddMissileTrail(GameObject projectilePrefab, float scale)
        {
            if (!projectilePrefab || !vanillaMissileGhost)
            {
                return;
            }

            var controller = projectilePrefab.GetComponent<RoR2.Projectile.ProjectileController>();
            var ghost = controller ? controller.ghostPrefab : null;
            if (!ghost)
            {
                return;
            }

            foreach (Transform child in vanillaMissileGhost.transform)
            {
                bool emitsParticles = child.GetComponentInChildren<ParticleSystem>(true) || child.GetComponentInChildren<TrailRenderer>(true);
                if (!emitsParticles)
                {
                    continue;
                }

                var copy = Object.Instantiate(child.gameObject, ghost.transform, false);
                copy.name = "SamusTrail_" + child.name;
                copy.transform.localScale = child.localScale * scale;
            }
        }
    }

    public class SamusLightFlash : MonoBehaviour
    {
        public Light light;
        public float startIntensity;
        public float duration = 0.25f;
        private float age;

        private void Update()
        {
            age += Time.deltaTime;
            float remaining = 1f - Mathf.Clamp01(age / duration);
            light.intensity = startIntensity * remaining * remaining;
            if (age >= duration)
            {
                Destroy(gameObject);
            }
        }
    }

    // Spawns a bigger explosion where a projectile ends up. Only the server spawns it, and it is replicated to everyone.
    public class SamusImpactBurst : MonoBehaviour
    {
        public float baseScale = 3f;
        public float scalePerTransformScale;

        private void OnDestroy()
        {
            if (!NetworkServer.active || !gameObject.scene.isLoaded)
            {
                return;
            }

            SamusEffects.Burst(transform.position, baseScale + scalePerTransformScale * transform.localScale.x);
        }
    }
}
