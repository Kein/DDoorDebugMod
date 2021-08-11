using System.Collections.Generic;
using UnityEngine;
using static Damageable;

namespace DDoorDebug.Model
{
    public class DDoorDebugData
    {
        // Scenes
        public string curActiveScene = string.Empty;
        public string lastActiveScene = string.Empty;
        public string[] allScenes;

        // Player data
        public Damageable dmgObject;
        public WeaponControl wpnObject;
        public WeaponAttackReferences wpnRefs;
        public _ArrowPower magicRefs;
        public PlayerMovementControl movCtrlObject;
        public Rigidbody plrRBody;
        public DamageData lastDamage;
        public SceneCP lastCheckPoint;
        public float lastSave;
        public float lastVelocity;
        public Queue<float> velSamples;
        public Queue<Vector3> posHistSamples;
        public Vector3 lastPosHistSample;
        public float lastVelSampleTime;
        public float lastPosHisSampleTime;
        public string[] bossKeys = new string[] { "bosskill_forestmother", "c_frogdead", "gd_frog_end", "c_grandead", "c_yetidead", "c_oldcrowdead", "c_loddead" };
        public string[] bossesIntroKeys = new string[] { "crowboss_intro_watched", "yetiboss_cutscene_watched", "grandma_fight_intro_seen", "grandma_boss_intro_watched", "frog_ghoul_intro", "frog_boss_intro_seen", "frogboss_cutscene_watched" };

        // References
        public Dictionary<int, string> dmgTypes;
        public List<DamageableRef> damageables;
    }
        public struct SceneCP
        {
            public int hash;
            public Vector3 pos;
        }

        public struct DamageData
        {
            public float dmg;
            public float poiseDmg;
            public DamageType type;
            public DamageData(float d, float p, DamageType t)
            {
                dmg = d;
                poiseDmg = p;
                type = t;
            }
        }

}
