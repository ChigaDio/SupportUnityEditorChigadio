using System;
using System.Collections.Generic;

public enum CharacterAnimStateEnum
{
    Base_Layer_Idle_01,
    Base_Layer_Run_01,
}

public class CharacterAnimState : BaseAnimState<CharacterAnimStateEnum>
{
    static CharacterAnimState()
    {
        StateNames = new Dictionary<CharacterAnimStateEnum, string>
        {
            { CharacterAnimStateEnum.Base_Layer_Idle_01, "Base Layer.Idle_01" },
            { CharacterAnimStateEnum.Base_Layer_Run_01, "Base Layer.Run_01" },
        };
    }

    protected override void InitializeStateNames() { }
}
// Parameters:
