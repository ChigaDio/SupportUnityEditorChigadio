using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class GameAnimatorController<TState, TEnum>
    where TState : BaseAnimState<TEnum>, new()
    where TEnum : struct, Enum
{
    private Animator animator;
    private GameObject target;

    // 現在の状態
    private TEnum currentState;
    private TState stateAnim = new TState();
    private bool isPlaying;
    public bool IsPlaying => isPlaying;

    private float speed = 1f; // Default speed
    public float Speed
    {
        get => speed;
        set
        {
            speed = Mathf.Max(0f, value); // Prevent negative or zero speed
            if (animator != null)
            {
                // Update animator speed immediately, accounting for reverse playback
                animator.speed = isPlaying && playInReverse ? -speed : speed;
            }
        }
    }

    private bool playInReverse; // Track reverse state for speed updates
    private CancellationTokenSource cts;

    // 初期化
    public void SetUp(GameObject target)
    {
        this.target = target;
        if (animator == null)
        {
            animator = target.GetComponentInChildren<Animator>();
        }

        cts = new CancellationTokenSource();

        // targetの破壊を監視してUniTaskをキャンセル
        UniTask.Void(async () =>
        {
            await UniTask.WaitUntil(() => target == null, cancellationToken: cts.Token);
            cts.Cancel();
        });
    }

    // 同期再生（UniTaskを使って非同期的に処理）
    public void Play(TEnum state, bool playInReverse = false, float transitionDuration = 0.25f, Action onComplete = null)
    {
        UniTask.Void(async () =>
        {
            await PlayInternalAsync(state, playInReverse, transitionDuration);
            onComplete?.Invoke();
        });
    }

    // 非同期再生（UniTask使用）
    public async UniTask PlayAsync(TEnum state, bool playInReverse = false, float transitionDuration = 0.25f, Action onComplete = null)
    {
        await PlayInternalAsync(state, playInReverse, transitionDuration);
        onComplete?.Invoke();
    }

    private async UniTask PlayInternalAsync(TEnum state, bool playInReverse, float transitionDuration)
    {
        // 既存のアニメーションをキャンセル
        if (isPlaying)
        {
            cts?.Cancel();
            cts?.Dispose();
            cts = new CancellationTokenSource();
        }

        if (cts.IsCancellationRequested) return;

        string stateName = stateAnim.GetStateName(state);
        int stateID = Animator.StringToHash(stateName);

        this.playInReverse = playInReverse; // Store reverse state
        // 再生速度を設定（正: speed, 逆: -speed）
        animator.speed = playInReverse ? -speed : speed;

        // 逆再生の場合、クリップの最後から開始
        if (playInReverse)
        {
            animator.Play(stateID, 0, 1f);
        }
        else
        {
            animator.CrossFade(stateID, transitionDuration);
        }

        currentState = state;
        isPlaying = true;

        try
        {
            // アニメーション終了を待機（逆再生の場合は0以下、正再生は1以上）
            await UniTask.WaitUntil(() => IsFinished(stateName, playInReverse), cancellationToken: cts.Token);
        }
        catch (OperationCanceledException)
        {
            // キャンセルされた場合
        }
        finally
        {
            isPlaying = false;
            animator.speed = speed; // Reset to current speed (not necessarily 1f)
            this.playInReverse = false; // Reset reverse state
        }
    }

    // ストップ
    public void Stop()
    {
        animator.StopPlayback();
        isPlaying = false;
        animator.speed = speed; // Maintain current speed setting
        playInReverse = false; // Reset reverse state
        cts?.Cancel();
        cts?.Dispose();
        cts = new CancellationTokenSource();
    }

    // アニメーションが終了したかチェック
    public bool IsFinished(string stateName, bool playInReverse = false)
    {
        if (stateName == null || animator == null) return true;
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

        if (playInReverse)
        {
            return stateInfo.IsName(stateName) && stateInfo.normalizedTime <= 0f;
        }
        return stateInfo.IsName(stateName) && stateInfo.normalizedTime >= 1.0f;
    }

    // 現在の状態取得
    public TEnum GetCurrentState() => currentState;
}