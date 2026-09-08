
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FirstScene : MonoBehaviour
{
    public GameObject startCanvas;
    public GameObject modeCanvas;
    public GameObject saveLoadCanvas;
    // Start is called before the first frame update
    void Start()
    {
        startCanvas.SetActive(true);
        modeCanvas.SetActive(false);
        saveLoadCanvas.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void StartButton()
    {
        //이 버튼을 누르면 이 캔버스는 비활성화 되고 다음 캔버스가 열린다.
        startCanvas.SetActive(false);
        modeCanvas.SetActive(true);
    }

    public void ModeButton_Normal()
    {
        saveLoadCanvas.SetActive(true);
        modeCanvas.SetActive(false);
    }
    public void ModeButton_TheRuler()
    {
        modeCanvas.SetActive(false);
        //지배자모드시작 최근에 자동저장된 기록을 불러온다 메인화면으로 넘어간다.
    }
}
