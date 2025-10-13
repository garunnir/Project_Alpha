using Garunnir;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.Wrappers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using DialogueDatabase = PixelCrushers.DialogueSystem.Wrappers.DialogueDatabase;
using DialogueSystemController = PixelCrushers.DialogueSystem.Wrappers.DialogueSystemController;

public class UIMapController : MonoBehaviour
{
    UIMapViewer view;
    MapContainer m_currentMap;
    MapEntry m_currentEntry;
    public MapEntry GetCurrentMapEntry()=>m_currentEntry;
    public MapContainer GetCurrentMap()=> m_currentMap;
    public event UnityAction<MapEntry> MapEntered;
    #region map
    public List<Actor> mapActorCache;
    private CustomDialogueSystemController dialogueSystemController;
    //맵 정보에서 타겟을 찾는다
    public List<Actor> FindActorOnMap(MapEntry entry)
    {
        return dialogueSystemController.masterDatabase.actors.FindAll(x => Field.LookupInt(x.fields, ConstDataTable.Map.Pos) == entry.MapID && Field.LookupInt(x.fields, ConstDataTable.Map.Pos) == entry.id);
    }
    public void Map_CharSelect(MapEntry entry)
    {
        if (mapActorCache == null || mapActorCache.Count == 0)
            mapActorCache = FindActorOnMap(entry);
        if (mapActorCache.Count > 0)
        {
            DialogueEntry dentry = dialogueSystemController.GetBasicDialogueEntry(CustomDialogueSystemController.Execute.Conversation);
            dentry.outgoingLinks.Clear();// 대화하기 버튼 누르면 강제로 연결함.
            List<string> strings = mapActorCache.ConvertAll(x => x.Name);
            dialogueSystemController.ConnectActorDialogueToResponseButton(dentry, mapActorCache);
        }
    }
    public void Map_FindCharInMap(MapEntry entry)
    {
        if (mapActorCache == null)
            mapActorCache = FindActorOnMap(entry);
        bool finded = mapActorCache != null || mapActorCache.Count == 0;
        dialogueSystemController.SelectionEnable(CustomDialogueSystemController.Execute.Conversation, finded);
        if (finded)
        {
            dialogueSystemController.conversationView.SelectedResponseHandler += dialogueSystemController.ChangeBtn;
            Map_CharSelect(entry);
        }
        else
        {
            dialogueSystemController.conversationView.SelectedResponseHandler -= dialogueSystemController.ChangeBtn;
        }
        //대화 출력 구문?
        dialogueSystemController.UpdateResponses();
    }
    #endregion


    private void Awake()
    {
        dialogueSystemController = FindAnyObjectByType<CustomDialogueSystemController>();
        MapEntered += Map_FindCharInMap;
        if (FindAnyObjectByType<GameManager>())
            GameManager.Instance.GetResourceManager().ResourceLoadDoneEvent += () =>
            {
                dialogueSystemController.Init();
                //StartConversation(basicConv.Title);
                dialogueSystemController.StartConversation(dialogueSystemController.masterDatabase.GetConversation(1).Title);
            };
        view = GetComponent<UIMapViewer>();
        m_currentMap = GameManager.Instance.GetResourceManager().GetMap(0);
        view.MoveEvent += MoveTo;
        view.CreatedEvent += CreateOther;
        MapEntered += ShowBG;
    }
    void Start()
    {
        ShowMap(m_currentMap);
    }
    private void ShowBG(MapEntry entry)
    {
        if (entry.backGroundTexture == null) return;
        RawImage img = GameManager.Instance.GetUIManager().GetBackground();
        img.texture = entry.backGroundTexture;
        //이미지 비율을 비교해서 적용한다.
        //원본 이미지 비율을 가져옴
        //지금 적용되어있는 랙트를 비교
        //원본이미지 폭에따라 길이를 재적용
        UIUtility.AdjustSize(GameManager.Instance.GetUIManager().GetUpperRect(), img.rectTransform, img.texture);
    }
    private void ShowMap(MapContainer container)
    {
        if (GameManager.Instance.GetResourceManager().MapIsExist()|| container==null)
        {
            return;
        }
        m_currentMap = container;
        view.createLimit = container.mapEntries.Count() * 3;
        MapEntry First = container.GetFirstMapEntry();
        ShowBG(First);
        m_currentEntry = First;
        view.ShowMap(First.Title,First.id);
    }
    void MoveTo(int entryid, bool ignoreBridge = false)
    {
        if (!ignoreBridge && !m_currentEntry.IsConnectedWith(entryid)) return;
        //셀렉트를 해당 아이디 위치로 이동한다
        view.SelectPositionMoveTo(entryid);
        m_currentEntry = m_currentMap.GetMapEntry(entryid);
        MapEntered?.Invoke(m_currentEntry);
        view.AlignCenter();
        //해당 셀렉트안의 이벤트를 발생시킨다.
    }
    void CreateOther(int entryid)
    {
        int target;
        MapEntry entry = m_currentMap.GetMapEntry(entryid);
        Vector2 tv2 = view.mapdic[entryid].transform.position;
        if (entry.IsExistCardinalPoints())
        {
            if (entry.postion.upID != -1)
            {
                Vector2 v2 = tv2;
                target = entry.postion.upID;
                v2.y += view.square / 2f + view.spacing / 2f;
                if (view.prevlist.Where(x => x.anchoredPosition == v2).Count() == 0)
                    view.GenerateBridge(v2, UIMapViewer.CP.updown);
                v2.y += view.square / 2f + view.spacing / 2f;
                view.CreateSprite(target.ToString(), target, v2);
            }
            if (entry.postion.downID != -1)
            {
                Vector2 v2 = tv2;
                target = entry.postion.downID;
                v2.y -= view.square / 2f + view.spacing / 2f;
                if (view.prevlist.Where(x => x.anchoredPosition == v2).Count() == 0)
                    view.GenerateBridge(v2, UIMapViewer.CP.updown);
                v2.y -=     view.square / 2f + view.spacing / 2f;
                view.CreateSprite(target.ToString(), target, v2);
            }
            if (entry.postion.leftID != -1)
            {
                Vector2 v2 = tv2;
                target = entry.postion.leftID;
                v2.x -= view.square / 2f + view.spacing / 2f;
                if (view.prevlist.Where(x => x.anchoredPosition == v2).Count() == 0)
                    view.GenerateBridge(v2, UIMapViewer.CP.leftright);
                v2.x -= view.square / 2f + view.spacing / 2f;
                view.CreateSprite(target.ToString(), target, v2);
            }
            if (entry.postion.rightID != -1)
            {
                Vector2 v2 = tv2;
                target = entry.postion.rightID;
                v2.x += view.square / 2f + view.spacing / 2f;
                if (view.prevlist.Where(x => x.anchoredPosition == v2).Count() == 0)
                    view.GenerateBridge(v2, UIMapViewer.CP.leftright);
                v2.x += view.square / 2f + view.spacing / 2f;
                view.CreateSprite(target.ToString(), target, v2);
            }
        }
    }
    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonUp(0))
        {
            //Vector3 wp = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, Camera.main.nearClipPlane));
            //마우스 스크린포인트를 가져온다
            //Debug.LogWarning(wp);

            //랙트유틸리티로 곂치는지 확인
            //마우스와 가장 근접한 랙트를 가져온다.
            //RectTransform closest = mapdic.Values.First();
            //foreach (RectTransform t in mapdic.Values)
            //{
            //    if (Vector2.Distance(wp, closest.rect.center) < Vector2.Distance(wp, t.rect.center))
            //    {
            //        closest = t;
            //    }
            //}
            //MoveTo(mapdic.Where(x => x.Value == closest).First().Key);
            view.CheckMoveable();

        }
    }
}
