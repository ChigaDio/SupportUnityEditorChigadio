using System;
using System.Collections.Generic;

public enum CameraTitleAnimStateEnum
{
    Base_Layer_Empty,
    Base_Layer_PushEnter,
}

public class CameraTitleAnimState : BaseAnimState<CameraTitleAnimStateEnum>
{
    static CameraTitleAnimState()
    {
        StateNames = new Dictionary<CameraTitleAnimStateEnum, string>
        {
            { CameraTitleAnimStateEnum.Base_Layer_Empty, "Base Layer.Empty" },
            { CameraTitleAnimStateEnum.Base_Layer_PushEnter, "Base Layer.PushEnter" },
        };
    }

    protected override void InitializeStateNames() { }
}
// Parameters:
