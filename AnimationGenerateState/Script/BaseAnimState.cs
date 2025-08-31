using System;
using System.Collections.Generic;

public abstract class BaseAnimState<TEnum> where TEnum : Enum
{
    // 派生クラスで初期化するDictionary
    protected static  Dictionary<TEnum, string> StateNames;

    // 派生クラスで実装を強制する抽象メソッド
    protected abstract void InitializeStateNames();

    public BaseAnimState()
    {

    }

    // 静的コンストラクタで初期化チェック
    static BaseAnimState()
    {
    }

    // State名を取得するメソッド
    public  string GetStateName(TEnum state)
    {
        if (StateNames.TryGetValue(state, out string name))
        {
            return name;
        }
        throw new ArgumentException($"State name not found for {state}");
    }
}