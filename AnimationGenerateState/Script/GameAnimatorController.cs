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

    // ���݂̏��
    private TEnum currentState;
    private TState stateAnim = new TState();
    private bool isPlaying;
    public bool IsPlaying => isPlaying;

    private CancellationTokenSource cts;

    // ������
    public void SetUp(GameObject target)
    {
        this.target = target;
        if (animator == null)
        {
            animator = target.GetComponentInChildren<Animator>();
        }

        cts = new CancellationTokenSource();

        // target�̔j����Ď�����UniTask���L�����Z��
        UniTask.Void(async () =>
        {
            await UniTask.WaitUntil(() => target == null, cancellationToken: cts.Token);
            cts.Cancel();
        });
    }

    // �����Đ��iUniTask���g���Ĕ񓯊��I�ɏ����j
    public void Play(TEnum state, float transitionDuration = 0.25f, Action onComplete = null)
    {
        UniTask.Void(async () =>
        {
            await PlayInternalAsync(state, transitionDuration);
            onComplete?.Invoke();
        });
    }

    // �񓯊��Đ��iUniTask�g�p�j
    public async UniTask PlayAsync(TEnum state, float transitionDuration = 0.25f, Action onComplete = null)
    {
        await PlayInternalAsync(state, transitionDuration);
        onComplete?.Invoke();
    }

    private async UniTask PlayInternalAsync(TEnum state, float transitionDuration)
    {
        // �����̃A�j���[�V�������L�����Z��
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
            // �A�j���[�V�����I����ҋ@
            await UniTask.WaitUntil(() => IsFinished(stateName), cancellationToken: cts.Token);
        }
        catch (OperationCanceledException)
        {
            // �L�����Z�����ꂽ�ꍇ
        }
        finally
        {
            isPlaying = false;
        }
    }

    // �X�g�b�v
    public void Stop()
    {
        animator.StopPlayback();
        isPlaying = false;
        cts?.Cancel();
        cts?.Dispose();
        cts = new CancellationTokenSource();
    }

    // �A�j���[�V�������I���������`�F�b�N
    public bool IsFinished(string stateName)
    {
        if (stateName == null || animator == null) return true;
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsName(stateName) && stateInfo.normalizedTime >= 1.0f;
    }

    // ���݂̏�Ԏ擾
    public TEnum GetCurrentState() => currentState;
}