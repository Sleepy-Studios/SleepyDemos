using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Hotfix.KagitKart
{
    /// 原作参数与八名角色的数据入口。
    [Serializable]
    public sealed class KagitKartSourceCatalog
    {
        public string sourceCommit;
        public KagitKartConfiguration config;
        public KagitKartCharacter[] roster;

        /// <summary>解析 Loader 提供的固定版本数据。</summary>
        /// <param name="json">SourceCatalog.json 内容。</param>
        /// <returns>通过基础领域校验的配置与角色。</returns>
        public static KagitKartSourceCatalog Parse(string json)
        {
            var source = JsonConvert.DeserializeObject<KagitKartSourceCatalog>(json,
                new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MaxDepth = 64 });
            if (source == null || source.sourceCommit != KagitKartCatalog.SourceCommit ||
                source.config?.kart == null || source.config.track == null || source.config.race == null ||
                source.config.classes == null || source.roster == null || source.roster.Length != 8)
            {
                throw new ArgumentException("纸艺卡丁车配置缺失或版本不匹配。", nameof(json));
            }
            foreach (var character in source.roster)
            {
                if (character?.stats == null)
                {
                    throw new ArgumentException("纸艺卡丁车角色属性缺失。", nameof(json));
                }
                KagitKartCatalog.CharacterName(character.id);
            }
            foreach (string speedClass in new[] { "80", "120", "200" })
            {
                if (!source.config.classes.TryGetValue(speedClass, out var value) || !(value.top > 0) || float.IsInfinity(value.top))
                {
                    throw new ArgumentException("纸艺卡丁车速度组别无效。", nameof(json));
                }
            }
            return source;
        }
    }

    /// 原作 KartTuning 参数，字段名与固定提交 JSON 一致。
    [Serializable]
    public sealed class KagitKartKartTuning
    {
        public float radius;
        public float statSpeed;
        public float statAccel;
        public float statHandling;
        public float statGrip;
        public float statWeight;
        public float statOffroad;
        public float accelTime;
        public float coastDecel;
        public float overCapDecel;
        public float brakeDecel;
        public double brakeDelay;
        public float softBrakeDecel;
        public float reverseMax;
        public float reverseAccel;
        public float yawLow;
        public float yawHigh;
        public float yawLowSpeed;
        public float yawFullSpeed;
        public float steerRampIn;
        public float steerRampOut;
        public float airYaw;
        public float grip;
        public float driftGrip;
        public float iceGrip;
        public float driftMinStart;
        public float driftMinHold;
        public float driftYawNeutral;
        public float driftYawIn;
        public float driftYawOut;
        public float driftChargeIn;
        public float driftChargeOut;
        public float[] driftTiers;
        public float driftVisualYaw;
        public float driftVisualSteer;
        public float driftSteerDeadzone;
        public float boostTopGain;
        public float playerBoostTopGain;
        public float boostRampTime;
        public Dictionary<string, float[]> boosts;
        public float draftRange;
        public float draftCone;
        public float draftMinSpeed;
        public float draftCharge;
        public float draftDecay;
        public float draftCooldown;
        public float offroadCap;
        public float sandCap;
        public float sandYaw;
        public float outCap;
        public float outRespawn;
        public float gravity;
        public float groundSnap;
        public float upDampHalfLife;
        public float airUpHalfLife;
        public float maxClimbVy;
        public float trickBefore;
        public float trickAfter;
        public float wallTangent;
        public float wallNormal;
        public float wallHardAngle;
        public float wallHardSpeed;
        public float wallSteerLock;
        public float wallScrape;
        public float wallImpactSpeed;
        public float wallImpactCooldown;
        public float wallAlign;
        public float wallInset;
        public float bumpImpulse;
        public float bumpSpeedLoss;
        public float bumpCooldown;
        public Dictionary<string, KagitKartSpinTuning> spins;
        public float graceAfterSpin;
        public float graceAfterRespawn;
        public float invincibleTopGain;
        public double respawnLift;
        public double respawnCarry;
        public double respawnDrop;
        public float respawnHeight;
        public float respawnSafeTime;
        public float respawnLateral;
        public float respawnLaunchWindow;
    }

    /// 原作 TrackTuningLayout 参数，字段名与固定提交 JSON 一致。
    [Serializable]
    public sealed class KagitKartTrackTuningLayout
    {
        public float lapMin;
        public float lapMax;
        public float radiusMargin;
        public float separationExtra;
        public float separationHeight;
        public float separationArcFactor;
        public float cornerRadius;
        public float tier3Arc;
        public float tier2Arc;
        public float tier3Min;
        public float tier2Min;
        public float tier2Max;
        public float rampStraight;
        public float rampStraightAngle;
        public float shortcutMin;
        public float shortcutMax;
        public float shortcutSaving;
        public float halfWidthMin;
        public float halfWidthMax;
        public float fogFarMin;
    }

    /// 原作 TrackTuning 参数，字段名与固定提交 JSON 一致。
    [Serializable]
    public sealed class KagitKartTrackTuning
    {
        public float spacing;
        public float denseSteps;
        public float gridCell;
        public float[] checkpoints;
        public float killDepth;
        public float wallHeight;
        public float curbWidth;
        public float curbHeight;
        public float padWidth;
        public float padLength;
        public float waterDrop;
        public float pageMargin;
        public float deskDrop;
        public float roadDashPeriod;
        public KagitKartTrackTuningLayout layout;
    }

    /// 原作 RaceTuning 参数，字段名与固定提交 JSON 一致。
    [Serializable]
    public sealed class KagitKartRaceTuning
    {
        public float countdownStep;
        public float[] startPerfect;
        public float[] startGood;
        public float finishWait;
        public float[] points;
        public float wrongWayTime;
        public float wrongWayOffTime;
        public float wrongWayDot;
        public float paceWindow;
        public float ghostHz;
    }

    /// 原作 RubberBandTuning 参数，字段名与固定提交 JSON 一致。
    [Serializable]
    public sealed class KagitKartRubberBandTuning
    {
        public float behindFrom;
        public float max;
        public float playerCap;
        public float aheadFrom;
        public float aheadMin;
    }

    /// 原作 CameraTuning 参数，字段名与固定提交 JSON 一致。
    [Serializable]
    public sealed class KagitKartCameraTuning
    {
        public float near;
        public float far;
        public float distance;
        public float boostDistance;
        public float height;
        public float lookAhead;
        public float lookHeight;
        public float yawHalfLife;
        public float driftYawShare;
        public float posHalfLife;
        public float heightHalfLife;
        public float airHeightHalfLife;
        public float hfovRest;
        public float hfovTop;
        public float hfovBoost;
        public float vfovMin;
        public float vfovMax;
        public float fovHalfLife;
        public float shakeDecay;
        public float shakeHit;
        public float shakeWall;
        public float lookBackDistance;
    }

    /// 原作 SpeedClass 参数，字段名与固定提交 JSON 一致。
    [Serializable]
    public sealed class KagitKartSpeedClass
    {
        public float top;
        public float rubberK;
        public float yawHigh;
    }

    /// 原作 SpinTuning 参数，字段名与固定提交 JSON 一致。
    [Serializable]
    public sealed class KagitKartSpinTuning
    {
        public float time;
        public float speed;
    }

    /// 原作 CharacterColors 参数，字段名与固定提交 JSON 一致。
    [Serializable]
    public sealed class KagitKartCharacterColors
    {
        public string body;
        public string accent;
        public string kart;
        public string detail;
    }

    /// 原作 CharacterStats 参数，字段名与固定提交 JSON 一致。
    [Serializable]
    public sealed class KagitKartCharacterStats
    {
        public float speed;
        public float accel;
        public float handling;
        public float weight;
        public float offroad;
    }

    /// 原作 CharacterVoice 参数，字段名与固定提交 JSON 一致。
    [Serializable]
    public sealed class KagitKartCharacterVoice
    {
        public float pitch;
    }

    /// 原作 Character 参数，字段名与固定提交 JSON 一致。
    [Serializable]
    public sealed class KagitKartCharacter
    {
        public string id;
        public string name;
        public string animal;
        public KagitKartCharacterColors colors;
        public KagitKartCharacterStats stats;
        public KagitKartCharacterVoice voice;
    }

    /// 原作比赛配置；不更改项目全局时间或画面设置。
    [Serializable]
    public sealed class KagitKartConfiguration
    {
        public Dictionary<string, KagitKartSpeedClass> classes;
        public KagitKartKartTuning kart;
        public KagitKartTrackTuning track;
        public KagitKartRaceTuning race;
        public KagitKartRubberBandTuning rubberBand;
        public KagitKartCameraTuning camera;
    }
}
