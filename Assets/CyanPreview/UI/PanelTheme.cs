using System;
using System.Collections.Generic;
using UnityEngine;

namespace CyanPreview
{
    // Owned once by the panel; no per-frame texture allocation or global skin mutation.
    public sealed class PanelTheme : IDisposable
    {
        public static readonly Color Background = new Color(.075f,.085f,.10f);
        public static readonly Color Accent = new Color(.32f,.89f,.83f);
        public readonly GUISkin Skin;
        public readonly GUIStyle Title, Small, Heading, Card, Primary, Selected, Badge, Error;
        readonly List<Texture2D> textures = new List<Texture2D>();
        public PanelTheme(GUISkin original)
        {
            Skin = UnityEngine.Object.Instantiate(original);
            var text = new Color(.89f,.92f,.94f);
            Skin.label.fontSize=14; Skin.label.normal.textColor=text; Skin.label.wordWrap=true;
            Skin.button = Button(new Color(.16f,.19f,.22f),text);
            Skin.textField = Button(new Color(.085f,.105f,.125f),text);
            Skin.textField.alignment=TextAnchor.MiddleLeft; Skin.textField.padding=new RectOffset(12,12,6,6);
            Skin.textField.focused.background=Texture(new Color(.17f,.28f,.30f));
            Skin.textField.focused.textColor=text;
            Skin.horizontalSlider.normal.background=Texture(new Color(.20f,.24f,.27f));
            Skin.horizontalSlider.fixedHeight=6; Skin.horizontalSlider.margin=new RectOffset(2,2,12,12);
            Skin.horizontalSliderThumb.normal.background=Texture(Accent);
            Skin.horizontalSliderThumb.hover.background=Texture(Color.white);
            Skin.horizontalSliderThumb.active.background=Skin.horizontalSliderThumb.hover.background;
            Skin.horizontalSliderThumb.fixedWidth=14; Skin.horizontalSliderThumb.fixedHeight=14;
            Skin.horizontalSliderThumb.overflow=new RectOffset(0,0,4,4);
            Skin.verticalScrollbar.fixedWidth=9;
            Skin.verticalScrollbar.normal.background=Texture(Background);
            Skin.verticalScrollbarThumb.normal.background=Texture(new Color(.24f,.30f,.33f));
            Skin.verticalScrollbarUpButton.fixedHeight=Skin.verticalScrollbarDownButton.fixedHeight=0;
            Title=new GUIStyle(Skin.label) {fontSize=25,fontStyle=FontStyle.Bold}; Title.normal.textColor=Accent;
            Heading=new GUIStyle(Skin.label) {fontSize=15,fontStyle=FontStyle.Bold};
            Small=new GUIStyle(Skin.label) {fontSize=12,wordWrap=true}; Small.normal.textColor=new Color(.62f,.69f,.73f);
            Card=new GUIStyle {normal={background=Texture(new Color(.115f,.135f,.16f))},border=new RectOffset(8,8,8,8),padding=new RectOffset(16,16,14,14),margin=new RectOffset(0,0,0,10)};
            Primary=Button(Accent,new Color(.045f,.11f,.12f)); Primary.fontStyle=FontStyle.Bold;
            Selected=Button(new Color(.13f,.32f,.33f),Accent); Selected.fontStyle=FontStyle.Bold;
            Badge=new GUIStyle(Selected) {fontSize=11,alignment=TextAnchor.MiddleCenter};
            Error=new GUIStyle(Card) {fontSize=12,wordWrap=true}; Error.normal.textColor=new Color(1f,.76f,.54f);
        }
        GUIStyle Button(Color fill,Color text)
        {
            var s=new GUIStyle(Skin.button) {fontSize=13,border=new RectOffset(8,8,8,8),padding=new RectOffset(10,10,7,7),margin=new RectOffset(2,2,3,3),alignment=TextAnchor.MiddleCenter,wordWrap=true};
            s.normal.background=Texture(fill); s.normal.textColor=text;
            s.hover.background=Texture(Color.Lerp(fill,Color.white,.12f)); s.hover.textColor=text;
            s.active.background=Texture(Color.Lerp(fill,Accent,.25f)); s.active.textColor=text;
            s.focused.background=Texture(Color.Lerp(fill,Accent,.2f)); s.focused.textColor=text;
            return s;
        }
        Texture2D Texture(Color color)
        {
            const int size=24;
            var t=new Texture2D(size,size,TextureFormat.RGBA32,false) {hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear};
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float dx=Mathf.Max(7-x,x-16),dy=Mathf.Max(7-y,y-16);
                float distance=new Vector2(Mathf.Max(0,dx),Mathf.Max(0,dy)).magnitude;
                var pixel=color; pixel.a*=Mathf.Clamp01(8-distance); t.SetPixel(x,y,pixel);
            }
            t.Apply(); textures.Add(t); return t;
        }
        public void Dispose()
        {
            foreach(var texture in textures) UnityEngine.Object.Destroy(texture);
            UnityEngine.Object.Destroy(Skin);
        }
    }
}
