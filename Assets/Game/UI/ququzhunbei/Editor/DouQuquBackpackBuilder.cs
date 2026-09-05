using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ZqyGameJam.UI.Ququzhunbei.Editor
{
    public static class DouQuquBackpackBuilder
    {
        const string Root = "Assets/Resources/Matchmaking";
        const string PrefabRoot = Root + "/Prefabs";
        const string Parts = PrefabRoot + "/Backpack172_469";
        const string TextureRoot = Root + "/Textures/Figma/Backpack172_469";

        [MenuItem("Tools/Cricket UI/Build Backpack 172-469")]
        public static void Build()
        {
            EnsureFolder(Parts);
            AssetDatabase.Refresh();
            var avatar1 = SpriteAt(TextureRoot + "/CricketAvatar_01.png");
            var avatar2 = SpriteAt(TextureRoot + "/CricketAvatar_02.png");
            var page = NewRect("BackpackSelectionScreen", new Vector2(1080, 1920), Vector2.zero);
            AddImage(page, null, new Color(0.914f, 0.871f, 0.773f, 1), false);

            var filters = NewRect("栏目", new Vector2(942, 176), new Vector2(0, 817));
            filters.transform.SetParent(page.transform, false);
            AddFilter(filters.transform, "全部", new Vector2(34, 26), new Vector2(218,107), new Color32(77,87,68,255));
            AddFilter(filters.transform, "机灵", new Vector2(328, 52), new Vector2(121,82), new Color32(222,153,186,255));
            AddFilter(filters.transform, "勇猛", new Vector2(498, 52), new Vector2(121,82), new Color32(211,81,83,255));
            AddFilter(filters.transform, "沉着", new Vector2(668, 52), new Vector2(121,82), new Color32(101,123,232,255));
            AddFilter(filters.transform, "威严", new Vector2(821, 49), new Vector2(121,82), new Color32(39,148,42,255));

            Vector2[] positions = { new(-342.5f, 748), new(-86.5f, 454), new(165.5f, 450), new(-90.5f, 750), new(161.5f, 750), new(-342.5f, 454), new(401.5f, 750), new(408.5f, 450), new(-342.5f, 171), new(-90.5f, 169), new(166.5f, 169) };
            string[] grades = { "极品","极品","极品","极品","极品","优品","极品","极品","良品","凡品","极品" };
            Color32[] cardColors = { new(112,18,18,255),new(112,18,18,255),new(112,18,18,255),new(112,18,18,255),new(112,18,18,255),new(170,48,168,255),new(112,18,18,255),new(112,18,18,255),new(109,135,221,255),new(191,191,191,255),new(112,18,18,255) };
            for (int i=0;i<positions.Length;i++)
            {
                var card = CreateCard("蛐蛐_"+(i+1), positions[i], grades[i], cardColors[i], i%2==0?avatar1:avatar2);
                card.transform.SetParent(page.transform, false);
            }
            var path = PrefabRoot + "/DouQuquMatchmaking_BackpackSelection.prefab";
            AssetDatabase.DeleteAsset(path);
            var saved = PrefabUtility.SaveAsPrefabAsset(page, path);
            Object.DestroyImmediate(page);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Selection.activeObject = saved;
            Debug.Log("Built modular BackpackSelectionScreen prefab with independent filter, card and leaf nodes.");
        }

        static GameObject CreateCard(string name, Vector2 pos, string grade, Color32 color, Sprite avatar)
        {
            var card = NewRect(name, new Vector2(243,317), pos);
            var baseGo = NewRect("底图", new Vector2(203,210), new Vector2(19,50)); baseGo.transform.SetParent(card.transform,false); AddImage(baseGo, null, color, true);
            var nameBg = NewRect("名称背景", new Vector2(159,57), new Vector2(20,187)); nameBg.transform.SetParent(baseGo.transform,false); AddImage(nameBg,null,new Color32(229,173,19,255),true);
            var nameText = NewText("名称文字", "白头狮", new Vector2(35,11), new Vector2(103,34), 32, new Color32(177,134,58,255)); nameText.transform.SetParent(nameBg.transform,false);
            var avatarGo = NewRect("头像", new Vector2(181,176), new Vector2(31,66)); avatarGo.transform.SetParent(card.transform,false); AddImage(avatarGo, avatar, Color.white, true);
            var gradeGo = NewRect("品级", new Vector2(46,155), new Vector2(4,81)); gradeGo.transform.SetParent(card.transform,false); AddImage(gradeGo, null, color.r==112?new Color32(229,173,19,255):color, true);
            var gradeText = NewText("品级文字", grade, new Vector2(1,26), new Vector2(30,110), 32, Color.white); gradeText.transform.SetParent(gradeGo.transform,false);
            SaveLeaf(card, name+"_底图", baseGo); SaveLeaf(card, name+"_头像", avatarGo); SaveLeaf(card, name+"_品级", gradeGo); SaveLeaf(card, name+"_名称背景", nameBg); SaveLeaf(card, name+"_名称文字", nameText); SaveLeaf(card, name+"_品级文字", gradeText);
            return card;
        }

        static void SaveLeaf(GameObject card, string file, GameObject leaf)
        {
            var clone = Object.Instantiate(leaf); clone.transform.SetParent(null); PrefabUtility.SaveAsPrefabAsset(clone, Parts+"/"+file+".prefab"); Object.DestroyImmediate(clone);
        }
        static void AddFilter(Transform parent,string label,Vector2 pos,Vector2 size,Color color)
        { var go=NewRect(label,size,pos); go.transform.SetParent(parent,false); AddImage(go,null,color,true); var txt=NewText(label+"文字",label,new Vector2(25,13),new Vector2(size.x-30,size.y-20),36,Color.white); txt.transform.SetParent(go.transform,false); SaveLeaf(go,label+"按钮",go); SaveLeaf(go,label+"文字",txt); }
        static GameObject NewRect(string name,Vector2 size,Vector2 pos){var go=new GameObject(name);var r=go.AddComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.pivot=new Vector2(.5f,.5f);r.sizeDelta=size;r.anchoredPosition=pos;return go;}
        static void AddImage(GameObject go,Sprite sprite,Color color,bool raycast){var i=go.AddComponent<Image>();i.sprite=sprite;i.color=color;i.raycastTarget=raycast;i.preserveAspect=false;}
        static GameObject NewText(string name,string value,Vector2 pos,Vector2 size,int fontSize,Color color){var go=NewRect(name,size,pos);var t=go.AddComponent<Text>();t.text=value;t.fontSize=fontSize;t.color=color;t.alignment=TextAnchor.MiddleCenter;t.horizontalOverflow=HorizontalWrapMode.Overflow;t.verticalOverflow=VerticalWrapMode.Overflow;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");return go;}
        static Sprite SpriteAt(string p){var imp=AssetImporter.GetAtPath(p) as TextureImporter;if(imp!=null){imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;imp.mipmapEnabled=false;imp.SaveAndReimport();}return AssetDatabase.LoadAssetAtPath<Sprite>(p);}
        static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;var parent=Path.GetDirectoryName(path).Replace("\\", "/");var name=Path.GetFileName(path);EnsureFolder(parent);AssetDatabase.CreateFolder(parent,name);}
    }
}


