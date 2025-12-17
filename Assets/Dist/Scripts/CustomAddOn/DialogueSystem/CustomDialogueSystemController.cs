using PixelCrushers.DialogueSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CharacterInfo = PixelCrushers.DialogueSystem.CharacterInfo;

public class CustomDialogueSystemController : DialogueSystemController
{
    public Conversation basicConv;
    public static CustomDialogueSystemController instance
    {
        get {
            return L_instance.Value;
        }
    }
    static Lazy<CustomDialogueSystemController> L_instance = new Lazy<CustomDialogueSystemController>(() => 
    {
        if (m_instance == null)
        {
            m_instance = FindAnyObjectByType<CustomDialogueSystemController>();
        }
        else
        {
            m_instance = new GameObject(typeof(CustomDialogueSystemController).Name,typeof(CustomDialogueSystemController)).GetComponent<CustomDialogueSystemController>();
        }
        return m_instance;
    });
    static CustomDialogueSystemController m_instance;

    public enum Execute
    {
        /// <summary>
        /// 조사하기
        /// </summary>
        Investigate = 6,
        /// <summary>
        /// 대화하기
        /// </summary>
        Conversation = 4,
    }

    public void Init()
    {
        basicConv = masterDatabase.GetConversation(1);
        customResponseTimeoutHandler += () => CharacterWaitResponce();
    }
    void CharacterWaitResponce()
    {
        //대화상대
        var actor = CurrentConversationState.subtitle.speakerInfo;
        Actor aactor = masterDatabase.GetActor(actor.id);
        var listener = CurrentConversationState.subtitle.listenerInfo;
        string value = aactor.LookupValue("Dialogue.Waiting");
        //어떻게 로컬라이징 할 것인지?
        InterceptDialogue(actor, listener, value);
    }
    public void SelectionEnable(Execute execute, bool boolean)
    {
        DialogueEntry dialogueEntry = GetBasicDialogueEntry(execute);
        dialogueEntry.conditionsString = boolean ? "true" : "false";
    }
    public DialogueEntry GetBasicDialogueEntry(Execute execute)
    {
        return basicConv.GetDialogueEntry((int)execute);
    }
    private void MoveToEntry(DialogueEntry entry)
    {
        conversationController.GotoState(conversationModel.GetState(entry));
    }
    public void AskToChar(string value)
    {
        Conversation conversation = masterDatabase.GetConversation(conversationModel.conversationTitle);
        string keyword = value;
        DialogueEntry entry = conversation.dialogueEntries.Find(x => x.Title == keyword);
        if (entry != null)
        {
            DialogueManager.StartConversation(conversationModel.conversationTitle, null, null, entry.id);
        }
        else
        {
            InterceptDialogue(5, 1, "모루겟소요");
            //아돈노 대사 출력
            //
        }
        //캐릭터한테 키워드로 말건다
        //캐릭터에 해당키워드가 존재하면 다음으로 넘어가고 아니면 잘 모르겠다는 대사를 출력시킨다.
        //키워드에 맞는 대화로 이동한다.
    }
    public void InterceptDialogue(int actor, int listener, string text)
    {
        CharacterInfo actorInfo = conversationModel.GetCharacterInfo(actor);
        CharacterInfo listenerInfo = conversationModel.GetCharacterInfo(listener);
        InterceptDialogue(actorInfo, listenerInfo, text);
    }
    public void InterceptDialogue(CharacterInfo actorInfo, CharacterInfo listenerInfo, string text)
    {
        //엑터가 대화중에 갑툭튀한다.
        DialogueEntry entry = currentConversationState.subtitle.dialogueEntry;
        FormattedText formattedText = FormattedText.Parse(text, masterDatabase.emphasisSettings);
        var prev = currentConversationState.subtitle;
        Subtitle subtitle = new Subtitle(actorInfo, listenerInfo, formattedText, "SetContinueMode(true);", "", entry);
        conversationView.dialogueUI.ShowSubtitle(subtitle);
        conversationModel.ForceNextStateToLinkToEntry(entry);
        //conversationController.currentState
        conversationView.FinishedSubtitleHandler += (x, y) => { Debug.LogError(x); };
    }
    /// <summary>
    /// 기본 선택창 표시
    /// </summary>
    private void StartConvPopBasicSelection()
    {
        StartConversation(basicConv.Title);
    }

    public void ChangeBtn(object sender, SelectedResponseEventArgs e)
    {
        UpdateResponses();
        //conversationView.SelectedResponseHandler -= ChangeBtn;
        //현상: 이 메서드를 발동시켰을때 자기자신을 체인에서 빼면 작동하지 않았다.
        //같은 것만 매칭됐을때 작동했다. 라는것은 체인지 버튼이 실행되면서 체인지버튼이 빠지게 하는것은 체인지 버튼이 작동하지 않게 한다는 이야기가 된다.
        //그것이 합당하려면 체인지 버튼이 실행될때 체인지버튼이 빠지는것과 동시에 실행하지 않게 된다는 말밖에 안되거나
        //대리자가 컴파일시점이나 그때쯤에 등록,해제가 된다면 말이 된다.
        //대리자 실행원리를 자세히 알 필요가 있다.
    }
    /// <summary>
    /// 캐릭터와 대화를 하기 위해, 캐릭터 대화에 해당하는 다이얼로그 데이터가 나타날 수 있게, 리스폰스 버튼에 다이얼로그 데이터를 삽입한다.
    /// </summary>
    /// <param name="dentry"></param>
    /// <param name="mapActor"></param>
    public void ConnectActorDialogueToResponseButton(DialogueEntry dentry, List<Actor> mapActor)
    {
        for (int i = 0; i < mapActor.Count; i++)
        {
            DialogueEntry created = AddResponse(dentry, mapActor[i].Name, "None()");
            int ownidx = Field.LookupInt(mapActor[i].fields,ConstDataTable.Actor.Conversation.Personalized);
            DialogueEntry copyTarget = databaseManager.DefaultDatabase.conversations[ownidx].GetFirstDialogueEntry();
            CopyLink(created, copyTarget);
        }
    }
    private void CopyLink(DialogueEntry created, DialogueEntry source)
    {
        created.outgoingLinks = new List<Link>(source.outgoingLinks);
        foreach (Link link in created.outgoingLinks)
        {
            link.originDialogueID = created.id;
            link.originConversationID = created.conversationID;
        }
    }
    void GenFakeResponse()
    {
        //가짜 리스폰스를 런타임에 가공해서 넣는다.
        //기존 리스폰스는 무조건 다음 선택지를 보여주게 세팅되어 있다.
        //이거를 인터셉트 하려고 했더니 안되네?
        //런타임에 선택지를 생성할 방법이 있을것 같은데..
        //conversationController.
        Conversation conv = basicConv;



    }
    private Link NewLink(DialogueEntry origin, DialogueEntry destination, ConditionPriority priority = ConditionPriority.Normal)
    {
        var newLink = new Link();
        newLink.originConversationID = origin.conversationID;
        newLink.originDialogueID = origin.id;
        newLink.destinationConversationID = destination.conversationID;
        newLink.destinationDialogueID = destination.id;
        newLink.isConnector = (origin.conversationID != destination.conversationID);
        newLink.priority = priority;
        return newLink;
    }
    //Conversation CreateConv()
    //{
    //    var fakeConversation = new Conversation();
    //    fakeConversation.id = -1;
    //    fakeConversation.fields = new List<Field>();
    //    fakeConversation.fields.Add(new Field("Title", "런타임실행", FieldType.Text));
    //    var entry = new DialogueEntry();
    //    entry.conversationID = fakeConversation.id;
    //    entry.id = 0;
    //    entry.fields = new List<Field>();
    //    entry.DialogueText = " ";
    //    entry.Sequence = "None()";
    //    entry.outgoingLinks.Add(new Link(fakeConversation.id, 0, fakeConversation.id, 1));
    //    fakeConversation.dialogueEntries.Add(entry);
    //    entry = new DialogueEntry();
    //    entry.conversationID = fakeConversation.id;
    //    entry.fields = new List<Field>();
    //    entry.id = 1;
    //    entry.DialogueText = " ";
    //    entry.Sequence = "Delay(2)";
    //    fakeConversation.dialogueEntries.Add(entry);
    //    return fakeConversation;
    //}
    DialogueEntry CreateEntry(int convid, string text, string sequence)
    {
        var entry = new DialogueEntry();
        entry.fields = new List<Field>();
        entry.ActorID = entry.ConversantID = 1;
        entry.id = basicConv.dialogueEntries.Last().id + 1;
        basicConv.dialogueEntries.Add(entry);
        entry.conversationID = convid;
        entry.DialogueText = text;
        entry.Sequence = sequence;
        DialogueLua.GetSimStatus(entry.conversationID, entry.id);//simstatus등록
                                                                 //DialogueLua.GetSimStatus(1, 1);//simstatus등록
        return entry;
    }
    void AddResponse(DialogueEntry mother, string[] item)
    {
        mother.outgoingLinks.Clear();
        for (int i = 0; i < item.Length; i++)
        {
            AddResponse(mother, item[i], "OpenCustomResponse()");
        }
    }
    /// <summary>
    /// 링크를 연결하는 것으로, 리스폰스 아이템을 추가한다.
    /// </summary>
    /// <param name="mother"></param>
    /// <param name="display"></param>
    /// <param name="sequence"></param>
    /// <returns></returns>
    DialogueEntry AddResponse(DialogueEntry mother, string display, string sequence)
    {
        var entry = CreateEntry(mother.conversationID, display, sequence);
        mother.outgoingLinks.Add(NewLink(mother, entry));
        return entry;
    }


    private void OnSelectEvent(object sender, SelectedResponseEventArgs e)
    {
        DialogueManager.instance.activeConversation = conversationController.activeConversationRecord;
        conversationController.GotoState(conversationModel.GetState(e.DestinationEntry));
    }

    private void StartConv()
    {
        StopConversation();
        StartConvPopBasicSelection();
    }
}
