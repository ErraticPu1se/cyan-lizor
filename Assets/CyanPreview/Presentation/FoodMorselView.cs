using System;
using UnityEngine;
namespace CyanPreview
{
    public sealed class FoodMorselView : IDisposable
    {
        readonly GameObject root;
        readonly Transform home;
        readonly SpriteRenderer renderer;
        readonly Texture2D texture;
        readonly Sprite sprite;
        readonly Material material;
        public FoodMorselView(Transform parent)
        {
            home=parent; root=new GameObject("Food morsel"); root.transform.SetParent(parent,false);
            texture=new Texture2D(7,5,TextureFormat.RGBA32,false) {filterMode=FilterMode.Point};
            var pixels=new Color32[35];
            for(int y=0;y<5;y++) for(int x=0;x<7;x++)
                if((y>0 && y<4 && x>0 && x<6) || (y==2 && (x==0 || x==6)))
                    pixels[y*7+x]=y>=3?new Color32(255,213,136,255):new Color32(196,118,72,255);
            texture.SetPixels32(pixels); texture.Apply();
            sprite=Sprite.Create(texture,new Rect(0,0,7,5),Vector2.one*.5f,.5f);
            material=new Material(Shader.Find("CyanPreview/Sprite")); material.mainTexture=texture;
            renderer=root.AddComponent<SpriteRenderer>(); renderer.sprite=sprite; renderer.sharedMaterial=material; renderer.sortingOrder=1000;
            root.SetActive(false);
        }
        public void Show(bool visible,Vector2 position)
        { root.transform.SetParent(home,false); root.SetActive(visible); root.transform.localPosition=position;
            root.transform.localRotation=Quaternion.identity; root.transform.localScale=Vector3.one; renderer.sortingOrder=1000; }
        public void Hold(Transform pet,Vector2 position,float angle,int order)
        { root.transform.SetParent(pet,false); root.SetActive(true); root.transform.localPosition=position;
            root.transform.localRotation=Quaternion.Euler(0,0,angle); root.transform.localScale=Vector3.one*.75f; renderer.sortingOrder=order; }
        public void Dispose()
        { UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(sprite); UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(material); }
    }
}
