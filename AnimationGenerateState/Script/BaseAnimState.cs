using System;
using System.Collections.Generic;

public abstract class BaseAnimState<TEnum> where TEnum : Enum
{
    // �h���N���X�ŏ���������Dictionary
    protected static  Dictionary<TEnum, string> StateNames;

    // �h���N���X�Ŏ������������钊�ۃ��\�b�h
    protected abstract void InitializeStateNames();

    public BaseAnimState()
    {

    }

    // �ÓI�R���X�g���N�^�ŏ������`�F�b�N
    static BaseAnimState()
    {
    }

    // State�����擾���郁�\�b�h
    public  string GetStateName(TEnum state)
    {
        if (StateNames.TryGetValue(state, out string name))
        {
            return name;
        }
        throw new ArgumentException($"State name not found for {state}");
    }
}