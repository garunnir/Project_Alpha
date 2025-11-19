using System.Collections;
using UnityEngine;
using TMPro;

public class InteractionCommandUI : UI
{
    [SerializeField] private TextMeshProUGUI keyText;
    [SerializeField] private TextMeshProUGUI actionText;

    public void Show(string key, string action)
    {
        keyText.text = key;
        actionText.text = action;
        gameObject.SetActive(true);
    }

    public override void Hide()
    {
        gameObject.SetActive(false);
        base.Hide();
    }

    public override IEnumerator AddHideAct()
    {
        throw new System.NotImplementedException();
    }

    public override IEnumerator AddShowAct()
    {
        throw new System.NotImplementedException();
    }
}
