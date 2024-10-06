using BepInEx;
using BoplFixedMath;
using HarmonyLib;
using Microsoft.SqlServer.Server;
using Mono.Unix.Native;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.ParticleSystem.PlaybackState;

namespace Pacman
{
    [BepInPlugin("com.maxgamertyper1.pacmanborder", "Pacman Border", "1.1.0")]
    public class Suffocated : BaseUnityPlugin
    {
        private void Log(string message)
        {
            Logger.LogInfo(message);
        }

        private void Awake()
        {
            // Plugin startup logic
            Log($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            DoPatching();
        }

        private void DoPatching()
        {
            var harmony = new Harmony("com.maxgamertyper1.pacmanborder");

            Patch(harmony, typeof(DestroyIfOutsideSceneBounds), "UpdateSim", "ProjectilePatch", true);
            Patch(harmony, typeof(BoplBody), "Update", "PlatformPatch", true);
            Patch(harmony, typeof(ShootScaleChange), "Shoot", "ScaleChangePatch", true);
            Patch(harmony, typeof(RopeHook), "UpdateSim", "RopePatch", true);
            Patch(harmony, typeof(ShootQuantum), "Shoot", "BlinkPatch", true);
            Patch(harmony, typeof(ShootDuplicator), "Shoot", "DuplicatorPatch", true);
            Patch(harmony, typeof(BlackHole), "Update", "BlackHolePatch", true);
        }

        private void OnDestroy()
        {
            Log($"Bye Bye From {PluginInfo.PLUGIN_GUID}");
        }

        private void Patch(Harmony harmony, Type OriginalClass , string OriginalMethod, string PatchMethod, bool prefix)
        {
            MethodInfo MethodToPatch = AccessTools.Method(OriginalClass, OriginalMethod); // the method to patch
            MethodInfo Patch = AccessTools.Method(typeof(Patches), PatchMethod);
            if (prefix)
            {
                harmony.Patch(MethodToPatch, new HarmonyMethod(Patch));
            }
            else
            {
                harmony.Patch(MethodToPatch, null, new HarmonyMethod(Patch));
            }
            Log($"Patched {OriginalMethod} in {OriginalClass.ToString()}");
        }
    }

    public class Patches
    {
        public static bool DuplicatorPatch(ref ShootDuplicator __instance, Vec2 firepointFIX, Vec2 directionFIX, ref bool hasFired, int playerId, bool alreadyHitWater = false)
        {
            if (__instance.maxDistance > 200)
            {
                __instance.maxDistance = (Fix)100L;
            }
            LayerMask mask = __instance.collisionMask;
            if (alreadyHitWater)
            {
                mask = __instance.collisionMaskNoWater;
            }
            RaycastInformation raycastInformation = DetPhysics.Get().PointCheckAllRoundedRects(firepointFIX);
            if (!raycastInformation)
            {
                raycastInformation = DetPhysics.Get().RaycastToClosest(firepointFIX, directionFIX, __instance.maxDistance, mask);
            }
            try
            {
                GameObject hit = raycastInformation.pp.fixTrans.gameObject;
                return true;
            }
            catch (Exception e)
            {
                Vec2 RayEnd = firepointFIX + new Vec2(directionFIX.x * __instance.maxDistance, directionFIX.y * __instance.maxDistance);
                Fix oldmax = __instance.maxDistance;
                if (RayEnd.x < SceneBounds.BlastZone_XMin || RayEnd.x > SceneBounds.BlastZone_XMax)
                {
                    AudioManager.Get().Play("laserShoot");
                    Vec2 NewPos;
                    Fix OldLength;
                    Fix NewLength;

                    if (RayEnd.x < SceneBounds.BlastZone_XMin)
                    {
                        NewLength = (Fix)Math.Abs((long)(RayEnd.x - SceneBounds.BlastZone_XMin));
                        OldLength = __instance.maxDistance - NewLength;
                        NewPos = new Vec2(SceneBounds.BlastZone_XMax - (Fix)1, RayEnd.y);

                    }
                    else
                    {
                        NewLength = RayEnd.x - SceneBounds.BlastZone_XMax;
                        OldLength = __instance.maxDistance - NewLength;
                        NewPos = new Vec2(SceneBounds.BlastZone_XMin + (Fix)1, RayEnd.y);

                    }
                    Vec2 NewEnd = NewPos + new Vec2(directionFIX.x * NewLength, directionFIX.y * NewLength);

                    __instance.maxDistance = NewLength;
                    __instance.Shoot(NewPos, directionFIX, ref hasFired, playerId, alreadyHitWater);
                    __instance.maxDistance = oldmax;

                    ParticleSystem body = UnityEngine.Object.Instantiate<ParticleSystem>(__instance.rayParticle);

                    ParticleSystem.ShapeModule shape = body.shape;
                    ParticleSystem.EmissionModule emission = body.emission;

                    ParticleSystem.Burst burst = emission.GetBurst(0);
                    shape.radius = ((float)NewLength + __instance.trailEffectOffset) * 0.5f;
                    body.transform.position = new Vector3((float)(NewPos.x + directionFIX.x * NewLength / (Fix)2f), (float)(NewPos.y + directionFIX.y * NewLength / (Fix)2f), 0.0f);
                    body.transform.right = (Vector2)directionFIX;
                    burst.count = Mathf.Max((float)NewLength * 0.5f, 2f);
                    emission.SetBurst(0, burst);
                    __instance.onHitParticle.transform.position = (Vector2)NewEnd;
                    body.Simulate(0.11f);
                    body.Play();
                    __instance.onHitParticle.Play();

                    __instance.spawnRayCastEffect((Vector2)firepointFIX, (Vector2)directionFIX, (float)__instance.maxDistance, false, false);
                    return false;
                }
                return true;
            }
        }

        public static void BlackHolePatch(ref BlackHole __instance)
        {
            GameObject parent = __instance.gameObject;
            FixTransform parentft = parent.GetComponent<FixTransform>();
            Vec2 parentpos = parentft.position;
            if (parentpos.x >= SceneBounds.BlastZone_XMax)
            {
                Vector3 newPos = new Vector3((float)SceneBounds.BlastZone_XMin + 1f, (float)parentpos.y);
                parentft.position = (Vec2)newPos;
            }
            else if (parentpos.x <= SceneBounds.BlastZone_XMin)
            {
                Vector3 newPos = new Vector3((float)SceneBounds.BlastZone_XMax - 1f, (float)parentpos.y);
                parentft.position = (Vec2)newPos;
            }
        }

        public static bool ProjectilePatch(ref DestroyIfOutsideSceneBounds __instance, ref Fix simDeltaTime)
        {
            FixTransform fixTrans = __instance.fixTrans;

            if (__instance.DestroyIfTooFarX && (fixTrans.position.x < SceneBounds.BlastZone_XMin || fixTrans.position.x > SceneBounds.BlastZone_XMax))
            {
                Vec2 pos = fixTrans.position;
                if (pos.x < SceneBounds.BlastZone_XMin)
                {
                    pos.x = SceneBounds.BlastZone_XMax - (Fix)1;
                }
                else
                {
                    pos.x = SceneBounds.BlastZone_XMin + (Fix)1;
                }
                fixTrans.position = pos;
                return false;
            }
            return true;
        }

        public static bool PlatformPatch(ref BoplBody __instance) // Bopl Body
        {
            Vec2 pos = __instance.position;
            if (pos.x < SceneBounds.BlastZone_XMin || pos.x > SceneBounds.BlastZone_XMax)
            {
                float LevelId = (float)GameSession.currentLevel;
                if (__instance.gameObject.name.Contains("ORBIT") || __instance.gameObject.name.Contains("AntiLockPlatform") && LevelId == 45 || __instance.gameObject.name == "ResizablePlatform(Clone)")
                {
                    return true;
                }
                DPhysicsRoundedRect shape = __instance.GetComponent<DPhysicsRoundedRect>();
                if (shape == null) return true;
                Vec2 Extents = shape.CalcExtents();
                if (pos.x < SceneBounds.BlastZone_XMin)
                {
                    pos.x = SceneBounds.BlastZone_XMax - (Fix)1 - Extents.x;
                }
                else
                {
                    pos.x = SceneBounds.BlastZone_XMin + (Fix)1 + Extents.x;
                }
                __instance.position = pos;
                return true;
            }
            return true;
        }

        public static bool ScaleChangePatch(ref ShootScaleChange __instance, ref Vec2 firepointFIX, ref Vec2 directionFIX, ref int playerId, ref bool alreadyHitWater, ref bool hasFired) // need to clone trail and add it to other side
        {
            if (__instance.maxDistance > 200)
            {
                __instance.maxDistance = (Fix)100L;
            }
            LayerMask mask = __instance.collisionMask;
            if (alreadyHitWater)
            {
                mask = __instance.collisionMaskNoWater;
            }
            RaycastInformation raycastInformation = DetPhysics.Get().PointCheckAllRoundedRects(firepointFIX);
            if (!raycastInformation)
            {
                raycastInformation = DetPhysics.Get().RaycastToClosest(firepointFIX, directionFIX, __instance.maxDistance, mask);
            }
            try
            {
                GameObject hit = raycastInformation.pp.fixTrans.gameObject;
                return true;
            }
            catch (Exception e)
            {
                Vec2 RayEnd = firepointFIX + new Vec2(directionFIX.x * __instance.maxDistance, directionFIX.y * __instance.maxDistance);
                Fix oldmax = __instance.maxDistance;
                if (RayEnd.x < SceneBounds.BlastZone_XMin || RayEnd.x > SceneBounds.BlastZone_XMax)
                {
                    AudioManager.Get().Play("laserShoot");
                    Vec2 NewPos;
                    Fix OldLength;
                    Fix NewLength;

                    if (RayEnd.x < SceneBounds.BlastZone_XMin)
                    {
                        NewLength = (Fix)Math.Abs((long)(RayEnd.x - SceneBounds.BlastZone_XMin));
                        OldLength = __instance.maxDistance - NewLength;
                        NewPos = new Vec2(SceneBounds.BlastZone_XMax - (Fix)1, RayEnd.y);

                    }
                    else
                    {
                        NewLength = RayEnd.x - SceneBounds.BlastZone_XMax;
                        OldLength = __instance.maxDistance - NewLength;
                        NewPos = new Vec2(SceneBounds.BlastZone_XMin + (Fix)1, RayEnd.y);

                    }
                    Vec2 NewEnd = NewPos + new Vec2(directionFIX.x * NewLength, directionFIX.y * NewLength);

                    __instance.maxDistance = NewLength;
                    __instance.Shoot(NewPos, directionFIX, ref hasFired, playerId, alreadyHitWater);
                    __instance.maxDistance = oldmax;


                    ParticleSystem TransmittedRay = UnityEngine.Object.Instantiate<ParticleSystem>(__instance.RaycastParticlePrefab);
                    ParticleSystem.ShapeModule shape = TransmittedRay.shape;
                    ParticleSystem.EmissionModule emission = TransmittedRay.emission;
                    ParticleSystem.Burst burst = emission.GetBurst(0);

                    shape.scale = new Vector3((float)NewLength, shape.scale.y, shape.scale.z);
                    TransmittedRay.transform.right = (Vector2)directionFIX;
                    TransmittedRay.transform.position = new Vector3((float)(NewPos.x + directionFIX.x * NewLength / (Fix)2f), (float)(NewPos.y + directionFIX.y * NewLength / (Fix)2f), 0.0f);

                    burst.count = (float)NewLength * __instance.rayDensity;
                    emission.SetBurst(0, burst);

                    ParticleSystem rayParticleChild = TransmittedRay.transform.GetChild(0).GetComponent<ParticleSystem>();

                    ParticleSystem.ShapeModule shape2 = rayParticleChild.shape;
                    ParticleSystem.EmissionModule emission2 = rayParticleChild.emission;
                    ParticleSystem.Burst burst2 = emission2.GetBurst(0);

                    shape2.scale = new Vector3((float)NewLength, shape2.scale.y, shape2.scale.z);
                    burst2.count = (float)NewLength * __instance.rayDensityChild;
                    emission2.SetBurst(0, burst2);

                    TransmittedRay.Simulate(0.16f);
                    TransmittedRay.Play();

                    __instance.hitParticle.transform.position = (Vector2)NewEnd;
                    __instance.hitParticle.Play();


                    __instance.spawnRayCastEffect((Vector2)firepointFIX, (Vector2)directionFIX, (float)__instance.maxDistance, false, false);
                    return false;
                }
                return true;
            }
        }

        public static bool BlinkPatch(ref ShootQuantum __instance, ref Vec2 firepointFIX, ref Vec2 directionFIX, ref int playerId, ref bool alreadyHitWater, ref bool hasFired)
        {
            if (__instance.maxDistance > 200)
            {
                __instance.maxDistance = (Fix)100L;
            }
            RaycastInformation raycastInformation = DetPhysics.Get().PointCheckAllRoundedRects(firepointFIX);
            if (!raycastInformation)
            {
                raycastInformation = DetPhysics.Get().RaycastToClosest(firepointFIX, directionFIX, __instance.maxDistance, __instance.collisionMask);
            }
            try
            {
                GameObject hit = raycastInformation.pp.fixTrans.gameObject;
                return true;
            }
            catch (Exception e)
            {
                Vec2 RayEnd = firepointFIX + new Vec2(directionFIX.x * __instance.maxDistance, directionFIX.y * __instance.maxDistance);
                Fix oldmax = __instance.maxDistance;
                if (RayEnd.x < SceneBounds.BlastZone_XMin || RayEnd.x > SceneBounds.BlastZone_XMax)
                {
                    AudioManager.Get().Play("fireRaygun");
                    Vec2 NewPos;
                    Fix OldLength;
                    Fix NewLength;

                    if (RayEnd.x < SceneBounds.BlastZone_XMin)
                    {
                        NewLength = (Fix)Math.Abs((long)(RayEnd.x - SceneBounds.BlastZone_XMin));
                        OldLength = __instance.maxDistance - NewLength;
                        NewPos = new Vec2(SceneBounds.BlastZone_XMax - (Fix)1, RayEnd.y);

                    }
                    else
                    {
                        NewLength = RayEnd.x - SceneBounds.BlastZone_XMax;
                        OldLength = __instance.maxDistance - NewLength;
                        NewPos = new Vec2(SceneBounds.BlastZone_XMin + (Fix)1, RayEnd.y);

                    }
                    Vec2 NewEnd = NewPos + new Vec2(directionFIX.x * NewLength, directionFIX.y * NewLength);

                    __instance.maxDistance = NewLength;
                    __instance.Shoot(NewPos, directionFIX, ref hasFired, playerId, alreadyHitWater);
                    __instance.maxDistance = oldmax;

                    ParticleSystem particleSystem = alreadyHitWater ? __instance.rayParticle2 : __instance.rayParticle;
                    ParticleSystem ClonedBeam = UnityEngine.Object.Instantiate<ParticleSystem>(particleSystem);
                    ParticleSystem.ShapeModule shape = ClonedBeam.shape;
                    ParticleSystem.EmissionModule emission = ClonedBeam.emission;
                    ParticleSystem.Burst burst = emission.GetBurst(0);

                    shape.scale = new Vector3((float)NewLength, shape.scale.y, shape.scale.z);
                    ClonedBeam.transform.right = (Vector3)directionFIX;
                    ClonedBeam.transform.position = new Vector3((float)(NewPos.x + directionFIX.x * NewLength / (Fix)2f), (float)(NewPos.y + directionFIX.y * NewLength / (Fix)2f), 0.0f);
                    burst.count = (float)NewLength * __instance.rayDensity;
                    emission.SetBurst(0, burst);
                    ClonedBeam.Play();

                    __instance.hitParticle.Stop();
                    __instance.hitParticle.transform.position = new Vector2((float)(NewPos.x + directionFIX.x * NewLength), (float)(NewPos.y + directionFIX.y * NewLength));
                    __instance.hitParticle.Play();
                    return true;
                }
                return true;
            }
        }

        public static void RopePatch(ref RopeHook __instance, ref Fix SimDeltaTime)
        {
            if (__instance.hitbox.position.x < SceneBounds.BlastZone_XMin || __instance.hitbox.position.x > SceneBounds.BlastZone_XMax)
            {
                if (__instance.hitbox.position.x < SceneBounds.BlastZone_XMin)
                {
                    __instance.hitbox.position = new Vec2(SceneBounds.BlastZone_XMax - (Fix)1, __instance.hitbox.position.y);
                }
                else
                {
                    __instance.hitbox.position = new Vec2(SceneBounds.BlastZone_XMin + (Fix)1, __instance.hitbox.position.y);
                }
            }
        }
    }
}
