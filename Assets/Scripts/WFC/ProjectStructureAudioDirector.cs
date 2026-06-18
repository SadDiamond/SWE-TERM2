using System.Collections;
using UnityEngine;

public class ProjectStructureAudioDirector : MonoBehaviour
{
    public CybergrindArenaDirector arenaDirector;
    public CybergrindTransitionController transitionController;
    public PlayerController player;
    public AudioSource audioSource;
    [Range(0f, 1f)] public float masterCueVolume = 0.18f;

    private int lastFloor = -1;
    private CybergrindArenaGenerator.ArenaMode? lastMode;
    private bool lastRewardPending;
    private bool lastShopReady;
    private bool lastPlayerDead;
    private bool lastRunComplete;
    private bool lastBossRewardRevealActive;
    private bool lastCoreAccessActive;
    private int lastBossPhase = -1;
    private string lastBossName = string.Empty;
    private Coroutine sequenceRoutine;
    private float nextCombatCueTime;
    private Transform cachedArenaRoot;
    private BasicEnemyAI[] cachedEnemies = System.Array.Empty<BasicEnemyAI>();

    private void Start()
    {
        if (arenaDirector == null)
            arenaDirector = FindAnyObjectByType<CybergrindArenaDirector>();
        if (transitionController == null)
            transitionController = FindAnyObjectByType<CybergrindTransitionController>();
        if (player == null)
            player = FindAnyObjectByType<PlayerController>();

        EnsureAudioSource();
        HookTransitionEvents();
        SnapshotState();
    }

    private void Update()
    {
        if (arenaDirector == null)
            arenaDirector = FindAnyObjectByType<CybergrindArenaDirector>();
        if (player == null)
            player = FindAnyObjectByType<PlayerController>();

        PollRunState();
    }

    private void EnsureAudioSource()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 1f;
    }

    private void HookTransitionEvents()
    {
        if (transitionController == null) return;

        transitionController.onTransitionStarted.RemoveListener(OnTransitionStarted);
        transitionController.onSwapMoment.RemoveListener(OnSwapMoment);
        transitionController.onTransitionFinished.RemoveListener(OnTransitionFinished);
        transitionController.onTransitionStarted.AddListener(OnTransitionStarted);
        transitionController.onSwapMoment.AddListener(OnSwapMoment);
        transitionController.onTransitionFinished.AddListener(OnTransitionFinished);
    }

    private void SnapshotState()
    {
        if (arenaDirector != null)
        {
            lastFloor = arenaDirector.floor;
            if (arenaDirector.generator != null)
                lastMode = arenaDirector.generator.arenaMode;
            lastRewardPending = arenaDirector.HasPendingReward();
            lastShopReady = arenaDirector.HasShopInteractionThisFloor();
            lastRunComplete = arenaDirector.RunComplete;
            lastBossRewardRevealActive = arenaDirector.IsBossRewardRevealActive;
            lastCoreAccessActive = arenaDirector.IsCoreAccessActive;
        }

        if (player != null)
            lastPlayerDead = player.isDead;

        BasicEnemyAI boss = FindCurrentBoss();
        if (boss != null)
        {
            lastBossPhase = boss.BossPhase;
            lastBossName = boss.displayName;
        }
    }

    private void PollRunState()
    {
        if (arenaDirector == null || arenaDirector.generator == null) return;

        if (transitionController == null)
        {
            transitionController = FindAnyObjectByType<CybergrindTransitionController>();
            HookTransitionEvents();
        }

        if (arenaDirector.floor != lastFloor || lastMode != arenaDirector.generator.arenaMode)
        {
            lastFloor = arenaDirector.floor;
            lastMode = arenaDirector.generator.arenaMode;
            PlayFloorStateCue(arenaDirector.generator.arenaMode);
        }

        bool rewardPending = arenaDirector.HasPendingReward();
        if (rewardPending && !lastRewardPending)
        {
            if (arenaDirector.generator.arenaMode == CybergrindArenaGenerator.ArenaMode.Boss)
                PlayBossRewardReadyCue();
            else
                PlayRewardReadyCue();
        }
        else if (!rewardPending && lastRewardPending)
            PlayRewardClaimedCue();
        lastRewardPending = rewardPending;

        bool bossRewardRevealActive = arenaDirector.IsBossRewardRevealActive;
        if (bossRewardRevealActive && !lastBossRewardRevealActive)
            PlayBossChamberUnlockCue();
        lastBossRewardRevealActive = bossRewardRevealActive;

        bool coreAccessActive = arenaDirector.IsCoreAccessActive;
        if (coreAccessActive && !lastCoreAccessActive)
            PlayCoreLinkOpenCue();
        lastCoreAccessActive = coreAccessActive;

        bool shopReady = arenaDirector.HasShopInteractionThisFloor();
        if (shopReady && !lastShopReady)
            PlayShopReadyCue();
        lastShopReady = shopReady;

        if (player != null)
        {
            if (player.isDead && !lastPlayerDead)
                PlayFailureCue();
            lastPlayerDead = player.isDead;
        }

        if (arenaDirector.RunComplete && !lastRunComplete)
            PlayCoreReachedCue();
        lastRunComplete = arenaDirector.RunComplete;

        BasicEnemyAI boss = FindCurrentBoss();
        if (boss == null)
        {
            if (!string.IsNullOrEmpty(lastBossName))
                PlayBossBrokenCue();
            lastBossName = string.Empty;
            lastBossPhase = -1;
            return;
        }

        if (!string.Equals(lastBossName, boss.displayName))
        {
            lastBossName = boss.displayName;
            PlayBossIntroCue(boss);
        }

        if (boss.BossPhase != lastBossPhase)
        {
            lastBossPhase = boss.BossPhase;
            if (lastBossPhase > 0)
                PlayBossPhaseCue(lastBossPhase);
        }
    }

    private BasicEnemyAI FindCurrentBoss()
    {
        Transform root = arenaDirector != null && arenaDirector.generator != null ? arenaDirector.generator.CurrentArenaRoot : null;
        if (root == null) return null;

        if (root != cachedArenaRoot)
        {
            cachedArenaRoot = root;
            cachedEnemies = root.GetComponentsInChildren<BasicEnemyAI>(true);
        }

        for (int i = 0; i < cachedEnemies.Length; i++)
        {
            BasicEnemyAI enemy = cachedEnemies[i];
            if (enemy != null && enemy.isBoss && !enemy.IsCombatResolved)
                return enemy;
        }

        return null;
    }

    private void OnTransitionStarted()
    {
        PlaySequence(
            Tone(210f, 0.10f, 0.45f, Waveform.Saw),
            Tone(280f, 0.08f, 0.38f, Waveform.Square));
    }

    private void OnSwapMoment()
    {
        PlayOneShot(Tone(180f, 0.12f, 0.52f, Waveform.Sine), 1f);
    }

    private void OnTransitionFinished()
    {
        PlaySequence(
            Tone(360f, 0.06f, 0.42f, Waveform.Sine),
            Tone(480f, 0.09f, 0.55f, Waveform.Sine));
    }

    private void PlayFloorStateCue(CybergrindArenaGenerator.ArenaMode mode)
    {
        switch (mode)
        {
            case CybergrindArenaGenerator.ArenaMode.Shop:
                PlaySequence(
                    Tone(440f, 0.05f, 0.35f, Waveform.Sine),
                    Tone(660f, 0.09f, 0.40f, Waveform.Sine));
                break;
            case CybergrindArenaGenerator.ArenaMode.Boss:
                PlaySequence(
                    Tone(160f, 0.12f, 0.5f, Waveform.Saw),
                    Tone(120f, 0.10f, 0.55f, Waveform.Saw),
                    Tone(200f, 0.08f, 0.42f, Waveform.Square));
                break;
            default:
                PlayOneShot(Tone(300f, 0.06f, 0.28f, Waveform.Sine), 0.8f);
                break;
        }
    }

    private void PlayRewardReadyCue()
    {
        PlaySequence(
            Tone(520f, 0.05f, 0.36f, Waveform.Sine),
            Tone(740f, 0.08f, 0.52f, Waveform.Sine));
    }

    private void PlayRewardClaimedCue()
    {
        PlaySequence(
            Tone(520f, 0.04f, 0.30f, Waveform.Sine),
            Tone(660f, 0.05f, 0.36f, Waveform.Sine),
            Tone(880f, 0.11f, 0.48f, Waveform.Sine));
    }

    private void PlayShopReadyCue()
    {
        PlaySequence(
            Tone(350f, 0.05f, 0.25f, Waveform.Sine),
            Tone(470f, 0.07f, 0.32f, Waveform.Sine));
    }

    public void PlayPickupCue(CybergrindPickup.PickupType pickupType)
    {
        switch (pickupType)
        {
            case CybergrindPickup.PickupType.Health:
                PlaySequence(
                    Tone(520f, 0.04f, 0.22f, Waveform.Sine),
                    Tone(640f, 0.06f, 0.28f, Waveform.Sine));
                break;
            default:
                PlaySequence(
                    Tone(760f, 0.04f, 0.2f, Waveform.Square),
                    Tone(920f, 0.06f, 0.24f, Waveform.Sine));
                break;
        }
    }

    public void PlayShopServiceCue(CybergrindShopStation.ShopService service)
    {
        switch (service)
        {
            case CybergrindShopStation.ShopService.Repair:
                PlaySequence(
                    Tone(420f, 0.05f, 0.24f, Waveform.Sine),
                    Tone(560f, 0.07f, 0.3f, Waveform.Sine));
                break;
            case CybergrindShopStation.ShopService.Refit:
                PlaySequence(
                    Tone(320f, 0.05f, 0.22f, Waveform.Square),
                    Tone(520f, 0.06f, 0.3f, Waveform.Sine),
                    Tone(640f, 0.08f, 0.34f, Waveform.Sine));
                break;
            case CybergrindShopStation.ShopService.Overclock:
                PlaySequence(
                    Tone(280f, 0.04f, 0.26f, Waveform.Saw),
                    Tone(420f, 0.05f, 0.3f, Waveform.Square),
                    Tone(760f, 0.1f, 0.36f, Waveform.Saw));
                break;
            default:
                PlaySequence(
                    Tone(500f, 0.04f, 0.22f, Waveform.Sine),
                    Tone(680f, 0.08f, 0.28f, Waveform.Square));
                break;
        }
    }

    public void PlayCombatImpactCue(bool kill, float damage)
    {
        float now = Time.unscaledTime;
        float cooldown = kill ? 0.08f : 0.045f;
        if (now < nextCombatCueTime) return;
        nextCombatCueTime = now + cooldown;

        if (kill)
        {
            PlayOneShot(Tone(720f, 0.05f, 0.18f, Waveform.Square), 0.75f);
            return;
        }

        float weight = Mathf.Clamp01(damage / 90f);
        float frequency = Mathf.Lerp(980f, 560f, weight);
        float duration = Mathf.Lerp(0.016f, 0.032f, weight);
        float amplitude = Mathf.Lerp(0.09f, 0.15f, weight);
        PlayOneShot(Tone(frequency, duration, amplitude, Waveform.Square), 0.55f);
    }

    private void PlayBossIntroCue(BasicEnemyAI boss)
    {
        float basePitch = boss.bossArchetype == BasicEnemyAI.BossArchetype.Sentinel ? 280f :
            boss.bossArchetype == BasicEnemyAI.BossArchetype.Striker ? 200f : 150f;
        PlaySequence(
            Tone(basePitch, 0.08f, 0.42f, Waveform.Saw),
            Tone(basePitch * 0.75f, 0.08f, 0.46f, Waveform.Saw),
            Tone(basePitch * 1.5f, 0.14f, 0.38f, Waveform.Square));
    }

    private void PlayBossPhaseCue(int phase)
    {
        float pitch = phase >= 2 ? 720f : 560f;
        PlaySequence(
            Tone(pitch, 0.05f, 0.42f, Waveform.Square),
            Tone(pitch * 0.84f, 0.05f, 0.48f, Waveform.Square),
            Tone(pitch * 1.12f, 0.08f, 0.44f, Waveform.Saw));
    }

    private void PlayBossBrokenCue()
    {
        PlaySequence(
            Tone(280f, 0.06f, 0.38f, Waveform.Saw),
            Tone(420f, 0.08f, 0.34f, Waveform.Sine),
            Tone(640f, 0.12f, 0.46f, Waveform.Sine));
    }

    private void PlayBossChamberUnlockCue()
    {
        PlaySequence(
            Tone(180f, 0.07f, 0.4f, Waveform.Saw),
            Tone(280f, 0.08f, 0.36f, Waveform.Square),
            Tone(420f, 0.1f, 0.42f, Waveform.Sine));
    }

    private void PlayBossRewardReadyCue()
    {
        PlaySequence(
            Tone(480f, 0.06f, 0.34f, Waveform.Sine),
            Tone(620f, 0.07f, 0.38f, Waveform.Sine),
            Tone(860f, 0.12f, 0.5f, Waveform.Square));
    }

    private void PlayFailureCue()
    {
        PlaySequence(
            Tone(220f, 0.10f, 0.46f, Waveform.Saw),
            Tone(170f, 0.12f, 0.44f, Waveform.Saw),
            Tone(120f, 0.18f, 0.40f, Waveform.Sine));
    }

    private void PlayCoreReachedCue()
    {
        PlaySequence(
            Tone(320f, 0.08f, 0.34f, Waveform.Sine),
            Tone(480f, 0.08f, 0.40f, Waveform.Sine),
            Tone(720f, 0.14f, 0.52f, Waveform.Sine));
    }

    private void PlayCoreLinkOpenCue()
    {
        PlaySequence(
            Tone(240f, 0.06f, 0.28f, Waveform.Sine),
            Tone(360f, 0.07f, 0.34f, Waveform.Sine),
            Tone(540f, 0.12f, 0.4f, Waveform.Saw));
    }

    private void PlaySequence(params AudioClip[] clips)
    {
        if (audioSource == null || clips == null || clips.Length == 0) return;

        if (sequenceRoutine != null)
            StopCoroutine(sequenceRoutine);
        sequenceRoutine = StartCoroutine(PlaySequenceRoutine(clips));
    }

    private IEnumerator PlaySequenceRoutine(AudioClip[] clips)
    {
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[i];
            if (clip == null) continue;
            audioSource.PlayOneShot(clip, masterCueVolume);
            yield return new WaitForSecondsRealtime(Mathf.Max(0.02f, clip.length * 0.9f));
        }

        sequenceRoutine = null;
    }

    private void PlayOneShot(AudioClip clip, float volumeScale)
    {
        if (audioSource == null || clip == null) return;
        audioSource.PlayOneShot(clip, masterCueVolume * volumeScale);
    }

    private enum Waveform
    {
        Sine,
        Square,
        Saw
    }

    private AudioClip Tone(float frequency, float duration, float amplitude, Waveform waveform)
    {
        int sampleRate = 44100;
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
        float[] samples = new float[sampleCount];
        amplitude = Mathf.Clamp01(amplitude);

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = 1f;
            float attack = Mathf.Min(0.02f, duration * 0.2f);
            float release = Mathf.Min(0.06f, duration * 0.35f);
            if (t < attack)
                envelope = attack <= 0.0001f ? 1f : t / attack;
            else if (t > duration - release)
                envelope = release <= 0.0001f ? 0f : Mathf.Clamp01((duration - t) / release);

            float phase = t * frequency;
            float sample = waveform switch
            {
                Waveform.Square => Mathf.Sign(Mathf.Sin(phase * Mathf.PI * 2f)),
                Waveform.Saw => 2f * (phase - Mathf.Floor(phase + 0.5f)),
                _ => Mathf.Sin(phase * Mathf.PI * 2f)
            };
            samples[i] = sample * amplitude * envelope;
        }

        AudioClip clip = AudioClip.Create($"PSCue_{frequency:0}_{duration:0.00}_{waveform}", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
