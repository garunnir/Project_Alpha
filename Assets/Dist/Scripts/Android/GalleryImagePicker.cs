using System.IO;
using UnityEngine;
using UnityEngine.UI;
namespace Garunnir
{
    public class GalleryImagePicker : MonoBehaviour
{
    public Button chanage;
    public RawImage img;
    public string filename;
        string _filepath;
    private void Awake()
    {
        chanage.onClick.AddListener(()=>ChangeImage(_filepath));
    }
    // 갤러리에서 이미지 선택 후 호출되는 콜백 메서드
    public void OnImagePicked(string imagePath)
    {
        //NativeGallery.SaveImageToGallery(byte[] mediaBytes, string album, string filename, MediaSaveCallback callback = null)
        //    NativeGallery.GetImageFromGallery(MediaPickCallback callback, string title = "", string mime = "image/*")
    }
    
    private void OnEnable()
    {
        if(string.IsNullOrEmpty(filename)) { _filepath = GameManager.charProfleImg +ConstDataTable.Actor.PlayerID; }
           else _filepath = Path.Combine(GameManager.charProfleImg + filename);
            if (File.Exists(_filepath))
        img.texture = Garunnir.Utillity.TextureIO.LoadImage(_filepath);
    }
    public void ChangeImage(string filepath)
    {
        NativeGallery.GetImageFromGallery((path) =>
        {
            print("CI:" + path);
            File.Copy(path, filepath, true);
            print("Dest:"+ filepath);
            ImgApply(path);
        }, "캐릭터 프로필 사진 선택", "image/*");
    }
    private void ImgApply(string path)
    {
        img.texture=Garunnir.Utillity.TextureIO.LoadImage(path);
    }

}
}
