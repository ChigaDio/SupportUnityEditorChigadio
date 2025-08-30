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
    public void Play(TEnum state, float transitionDuration = 0.25f, Action onComplete = null)
    {
        UniTask.Void(async () =>
        {
            await PlayInternalAsync(state, transitionDuration);
            onComplete?.Invoke();
        });
    }

    // 非同期再生（UniTask使用）
    public async UniTask PlayAsync(TEnum state, float transitionDuration = 0.25f, Action onComplete = null)
    {
        await PlayInternalAsync(state, transitionDuration);
        onComplete?.Invoke();
    }

    private async UniTask PlayInternalAsync(TEnum state, float transitionDuration)
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
        animator.CrossFade(stateID, transitionDuration);
        currentState = state;
        isPlaying = true;

        try
        {
            // アニメーション終了を待機
            await UniTask.WaitUntil(() => IsFinished(stateName), cancellationToken: cts.Token);
        }
        catch (OperationCanceledException)
        {
            // キャンセルされた場合
        }
        finally
        {
            isPlaying = false;
        }
    }

    // ストップ
    public void Stop()
    {
        animator.StopPlayback();
        isPlaying = false;
        cts?.Cancel();
        cts?.Dispose();
        cts = new CancellationTokenSource();
    }

    // アニメーションが終了したかチェック
    public bool IsFinished(string stateName)
    {
        if (stateName == null || animator == null) return true;
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsName(stateName) && stateInfo.normalizedTime >= 1.0f;
    }

    // 現在の状態取得
    public TEnum GetCurrentState() => currentState;
}