using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UIConstruction : MonoBehaviour
{
    private InputActions inputActions;
    [SerializeField] Button prevBtn;
    [SerializeField] Button nextBtn;
    [SerializeField] Button closeBtn;
    void Start()
    {
        prevBtn.onClick.AddListener(() => {
            Prev();
        });
        nextBtn.onClick.AddListener(() => {
            Next();
        });
        closeBtn.onClick.AddListener(() => {
            Close();
        });
        inputActions = new InputActions();
         
        inputActions.UI.Pagination.performed += OnPagination;
  
        inputActions.Enable();
    }
    public void OnPagination(InputAction.CallbackContext context)
{
    // 버튼을 눌렀을 때(Performed) 한 번만 실행되도록 체크
    if (context.performed)
    {
        float direction = context.ReadValue<float>();

        if (direction < 0)
        {
            Prev();
        }
        else if (direction > 0)
        {
            Next();
        }
    }
}
    private void Close()
    {
        Debug.Log("closeBtn clicked");
    }
    private void Prev()
    {
        Debug.Log("prevBtn clicked");
    }
    private void Next()
    {
        Debug.Log("nextBtn clicked");
    }
}
