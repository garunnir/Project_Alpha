using Garunnir;
using Garunnir.Runtime.ScriptableObject;
using PixelCrushers;
using PixelCrushers.DialogueSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

public class ResourceManager : MonoBehaviour
{
    List<string> tmpPathContainer = new List<string>();
    public event Action ResourceLoadDoneEvent;
    bool isResourceLoadDone = false;
    [SerializeField] List<MapContainer> mapContainer;

    #region CacheData
    public int GetCurLocID() => Localization.GetCurrentLanguageID(UILocalizationManager.instance.textTable);

#if !UNITY_EDITOR
        AndroidJavaClass ajc = new AndroidJavaClass("com.garunnir.File.FileController");
#endif
    private Dictionary<string, Texture2D> m_imgDic = new Dictionary<string, Texture2D>();
    public Texture2D GetImg(string key)
    {
        if (!m_imgDic.ContainsKey(key))
        {
            Debug.LogError("cantfind:" + key);
            return null;
        }
        return m_imgDic[key];
    }
    public Texture2D GetBG(string key)
    {
        key = "Img/Background/" + key + ".png";
        if (!m_imgDic.ContainsKey(key))
        {
            Debug.LogError("cantfind:" + key);
            return null;
        }
        return m_imgDic[key];
    }

    #endregion
    #region Image
    public void LoadAllImg()
    {
        InitCharImg(() => { LoadImgByDir(); print("Invoke"); ResourceLoadDoneEvent?.Invoke(); });
        print(tmpPathContainer.Count);
    }
    void LoadImgByDir()
    {
        //이미지를 경로로부터 가져온다.
        //경로를 어떻게?
        foreach (string key in tmpPathContainer)
        {
            string keyname = key.Replace(Application.persistentDataPath + "/", string.Empty);
#if !UNITY_EDITOR
                m_imgDic.Add(keyname, NativeGallery.LoadImageAtPath(key));
#else
            Texture2D tex = new Texture2D(8, 8);
            if (!File.Exists(key))
            {
                Debug.LogError("cantfind:" + key);
            }
            tex.LoadImage(File.ReadAllBytes(key));
            m_imgDic.Add(keyname, tex);
            Debug.LogWarning(keyname);
#endif
        }
    }

#if !UNITY_EDITOR
        void InitCharImg(Action done)
        {
            int totalTasks = 2; // 총 작업 수
            int completedTasks = 0; // 완료된 작업 수
            Action action= () => { 
                completedTasks++;
                Debug.LogWarning("com: "+completedTasks);
                if (completedTasks == totalTasks)
                {
                    Debug.Log("delete");
                    ajc.CallStatic<bool>("deleteDirectoryIn", "DCIM", "/_.PPResource");
                    done();
                }
            };
            ImgResourceInit("m_Background", action);
            ImgResourceInit("Character", action);
        }
        private void ImgResourceInit(string rpath,Action done)
        {
            //가지고 있던 이미지들을 전부 겔러리에 저장한다.
            //팝업이 뜨면 실패.
            //가지고 있는것이 있는지 먼저 판단
            //파일명부터 가져오자 경로를 긁어오는 메서드 있는지?
            Texture2D[] textures = Resources.LoadAll<Texture2D>("Img/" + rpath);
            //AndroidJavaClass ajc = new AndroidJavaClass("com.garunnir.File.FileController");
            string path = ajc.CallStatic<string>("GetFileEnvPath", "DCIM", "");
            //Debug.LogWarning(ajc.CallStatic<bool>("IsExistFileEnv", "DCIM", "/.PPResource/Background/portal.png"));
            print(ajc.CallStatic<bool>("CreateDirectoryIn", "DCIM", $"/.PPResource/{rpath}"));
            int tmpDone = textures.Length;
            bool check=false;
            foreach (Texture2D tex in textures)
            {
                tmpPathContainer.Add(path + $"/.PPResource/{rpath}/" + tex.name + ".png");

                if (ajc.CallStatic<bool>("IsExistFileEnv", "DCIM", $"/.PPResource/{rpath}/" + tex.name + ".png"))
                {
                    Debug.Log("already Exist: " + tex.name + ".png");
                    tmpDone--;
                    continue;
                }

                //File.WriteAllBytes(path+"/"+tex.name+ ".png", tex.GetRawTextureData());
                //if (textures.Last() != tex)
                //{

                //}
                //else
                //{

                //}
                check=true;
                NativeGallery.Permission permission =NativeGallery.SaveImageToGallery(tex, $".PPResource/{rpath}", tex.name + ".png", (x, y) =>
                {
                    print(y);//path
                    if (!x)
                    {
                        Debug.LogError("Save Failed");
                    }
                    tmpDone--;
                    ajc.CallStatic<bool>("MoveTo", "DCIM", $"/_.PPResource/{rpath}/" + tex.name + ".png", $"/.PPResource/{rpath}/" + tex.name + ".png");
                    if (tmpDone == 0)
                    {
                        done();
                    }
                });
                
            }
            //if (!File.Exists(path))
            //{
            //    Utillity.CheckFolderInPath(path);
            //}
           if (tmpDone == 0&&!check)
           done();

            print("end");
            //파일있는지 확인하고 없으면 그 경로에 파일을 복사해넣는다.
        }
#else
    async void InitCharImg(Action done)
    {
        List<Task> tasks = new List<Task>();

        // 비동기 작업들을 tasks 리스트에 추가
        tasks.Add(ImgResourceInit("Background"));
        tasks.Add(ImgResourceInit("Character"));

        // 모든 작업이 완료될 때까지 기다림
        await Task.WhenAll(tasks);

        done();
        Console.WriteLine("모든 작업이 완료되었습니다.");
    }
    async Task ImgResourceInit(string rpath)
    {
        //가지고 있던 이미지들을 전부 겔러리에 저장한다.
        //팝업이 뜨면 실패.
        //가지고 있는것이 있는지 먼저 판단
        //파일명부터 가져오자 경로를 긁어오는 메서드 있는지?
        Texture2D[] textures = Resources.LoadAll<Texture2D>("Img/" + rpath);
        string path = Garunnir.Utillity.PathUtility.EnsureDirectory(Application.persistentDataPath + ("/Img/" + rpath));
        print("start");
        foreach (var item in textures)
        {
            await File.WriteAllBytesAsync(path + "/" + item.name + ".png", Garunnir.Utillity.TextureIO.GetTextureBytesFromCopy(item));
            tmpPathContainer.Add(path + "/" + item.name + ".png");
        }
        print("end");
        //파일있는지 확인하고 없으면 그 경로에 파일을 복사해넣는다.
    }
#endif
    #endregion
    #region PreData
    [Header("PreData")]
    [SerializeField] EquipmentCollectionSO equipmentCollection;
    [SerializeField] SkillCollectionSO skillCollection;
    public EquipmentCollectionSO GetEquipData() => equipmentCollection;
    public SkillCollectionSO GetSkillData() => skillCollection;
    #endregion

    #region Utility
    //웨폰 갖다줄 유틸리티 구성
    /// <summary>
    /// 엑터의 필드정보에서 실제 무기 데이터를 가져온다.
    /// </summary>
    /// <param name="actor">필드에 무기 넘버를 가지고있는 액터</param>
    /// <returns></returns>
    public WeaponSO GetWeapon(Actor actor)
    {
        var actorid= Field.LookupInt(actor.fields, ConstDataTable.Equipment.Weapon);
        return (actorid!=-1)?equipmentCollection.equipment_weapon[actorid]:null;
    }
    public MapContainer GetMap(int idx)
    {
        return mapContainer[idx];
    }
    public bool MapIsExist()
    {
        return mapContainer!=null&&mapContainer.Count != 0;
    }
    #endregion
}
