using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AnimProbe : MonoBehaviour
{
    Animator anim;
    SpriteRenderer sr;

    void Awake()
    {
        anim = GetComponent<Animator>();
        sr = GetComponentInChildren<SpriteRenderer>();
    }

    void Update()
    {
        var st = anim.GetCurrentAnimatorStateInfo(0);
        var name = anim.GetCurrentAnimatorClipInfo(0);
        string clip = name.Length > 0 ? name[0].clip.name : "(no clip)";
        string sprite = sr ? (sr.sprite ? sr.sprite.name : "(no sprite)") : "(no SR)";
        Debug.Log($"[AnimProbe] stateHash:{st.shortNameHash}, clip:{clip}, sprite:{sprite}, layerW:{anim.GetLayerWeight(0)}");
    }
}