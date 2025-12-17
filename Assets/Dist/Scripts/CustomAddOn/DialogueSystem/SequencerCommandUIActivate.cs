
using UnityEngine;
using System.Collections;
using PixelCrushers.DialogueSystem;
using UnityEngine.Events;
interface ILuaUIAcitate
{
    public static event UnityAction<string, bool> Active;
    public void SetActive(string commend, bool activation);
    public static void Invoke(string commend, bool activation)
    {
        Active?.Invoke(commend, activation);
    }
}
namespace PixelCrushers.DialogueSystem.SequencerCommands
{

    public class SequencerCommandActivate : SequencerCommand
    { // Rename to SequencerCommand<YourCommand>
        string target;
        bool active;
        public void Awake()
        {
            target = GetParameterAs<string>(0, target);
            active = GetParameterAs<bool>(1, active);
            //입력받는 내용에 따라 활성화,비활성화 한다,
            ILuaUIAcitate.Invoke(target, active);
        }

        public void OnDestroy()
        {
            // Add your finalization code here. This is critical. If the sequence is cancelled and this
            // command is marked as "required", then only Awake() and OnDestroy() will be called.
            // Use it to clean up whatever needs cleaning at the end of the sequencer command.
            // If you don't need to do anything at the end, you can delete this method.
        }

    }

}
