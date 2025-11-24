using UnityEngine;
namespace Interactions
{
    [RequireComponent(typeof(Animator))]
    public class DoorInteractable : Interactable
    {
        [SerializeField] private bool isOpen = false;
        private Animator _doorAnimator;
        protected override void Awake()
        {
            base.Awake();
            _doorAnimator = GetComponent<Animator>();
        }
        public override bool CanInteract(GameObject interactor)
        {
            // 예: 키 체크, 잠금 상태 체크 등
            return true;
        }

        public override void Interact(GameObject interactor)
        {
            isOpen = !isOpen;
            _doorAnimator.SetBool("isOpen", isOpen);
            hintText = isOpen ? "문 닫기" : "문 열기";
        }

        public override void OnFocus(GameObject interactor)
        {
            base.OnFocus(interactor);
            // 아웃라인키거나, "E: 문 열기" 텍스트 요청
        }

        public override void OnUnfocus(GameObject interactor)
        {
            base.OnUnfocus(interactor);
            // 아웃라인 끄기 등
        }
    }

}

