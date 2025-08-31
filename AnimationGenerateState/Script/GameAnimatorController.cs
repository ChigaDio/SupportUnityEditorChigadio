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
    public void Play(TEnum state, bool playInReverse = false, float transitionDuration = 0.25f, Action onComplete = null)
    {
        UniTask.Void(async () =>
        {
            await PlayInternalAsync(state, playInReverse, transitionDuration);
            onComplete?.Invoke();
        });
    }

    // �񓯊��Đ��iUniTask�g�p�j
    public async UniTask PlayAsync(TEnum state, bool playInReverse = false, float transitionDuration = 0.25f, Action onComplete = null)
    {
        await PlayInternalAsync(state, playInReverse, transitionDuration);
        onComplete?.Invoke();
    }

    private async UniTask PlayInternalAsync(TEnum state, bool playInReverse, float transitionDuration)
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

        this.playInReverse = playInReverse; // Store reverse state
        // �Đ����x��ݒ�i��: speed, �t: -speed�j
        animator.speed = playInReverse ? -speed : speed;

        // �t�Đ��̏ꍇ�A�N���b�v�̍Ōォ��J�n
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
            // �A�j���[�V�����I����ҋ@�i�t�Đ��̏ꍇ��0�ȉ��A���Đ���1�ȏ�j
            await UniTask.WaitUntil(() => IsFinished(stateName, playInReverse), cancellationToken: cts.Token);
        }
        catch (OperationCanceledException)
        {
            // �L�����Z�����ꂽ�ꍇ
        }
        finally
        {
            isPlaying = false;
            animator.speed = speed; // Reset to current speed (not necessarily 1f)
            this.playInReverse = false; // Reset reverse state
        }
    }

    // �X�g�b�v
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

    // �A�j���[�V�������I���������`�F�b�N
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

    // ���݂̏�Ԏ擾
    public TEnum GetCurrentState() => currentState;
}