using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.VectorGraphics;

namespace ZqyGameJam.UI.QuquXiangqing.Editor
{
    /// <summary>Builds Figma 10:527 entirely from editable nested prefabs; the full-page PNG is reference-only.</summary>
    public static class QuquXiangqingModularFigmaBuilder
    {
        const string Root="Assets/Resources/Collection", Prefabs=Root+"/Prefabs", Parts=Prefabs+"/Parts";
        const string Textures=Root+"/Textures/Figma", PagePath=Prefabs+"/CricketDetail.prefab";
        const string ScenePath="Assets/Scenes/Preview/ququxiangqing.unity";
        const string ReferenceExport=Textures+"/QuquXiangqing_10_527.png";
        const string CricketPath=Textures+"/VioletCricketIllustration.png", LinePath=Textures+"/DecorativeLine.svg";
        const string FontPath="Assets/Resources/Fonts/DouQuquChinese SDF.asset";

        static readonly Vector2 Design=new Vector2(972,1336);
        static readonly Color Paper=new Color(.960784f,.92549f,.823529f,1);
        static readonly Color PaperBorder=new Color(.847059f,.772549f,.635294f,1);
        static readonly Color Gold=new Color(.772549f,.627451f,.34902f,1);
        static readonly Color Red=new Color(.619608f,.164706f,.168627f,1);
        static readonly Color LightGold=new Color(.898039f,.768627f,.560784f,1);
        static readonly Color Ink=new Color(.168627f,.156863f,.145098f,1);
        static readonly Color Muted=new Color(.419608f,.396078f,.372549f,1);
        static readonly Color Green=new Color(.176471f,.352941f,.152941f,1);
        static readonly Color Brown=new Color(.180392f,.109804f,.086275f,1);
        static readonly Color CardWhite=new Color(1,1,1,.94f);
        static TMP_FontAsset font;
        static Sprite rounded;

        sealed class Stat
        {
            public readonly string label,value;
            public Stat(string l,string v){label=l;value=v;}
        }
        static readonly Stat[,] Stats={
            {new Stat("重量","—"),new Stat("抓地力","—")},
            {new Stat("蓄力速度","—"),new Stat("蓄力时间上限","—")},
            {new Stat("耐力恢复速度","—"),new Stat("耐力上限","—")}
        };
        static GameObject statCardPrefab;

        [MenuItem("Tools/Cricket UI/Rebuild Ququ Detail (Figma 10:527, Modular)")]
        public static void Build()
        {
            EnsureFolders(); DeleteLegacy(); AssetDatabase.Refresh();
            statCardPrefab=null;
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            rounded=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            Sprite cricket=PrepareSprite(CricketPath);
            Sprite line=AssetDatabase.LoadAssetAtPath<Sprite>(LinePath);
            if(font==null)throw new FileNotFoundException("Missing Chinese TMP font",FontPath);
            if(cricket==null)throw new FileNotFoundException("Missing cricket illustration",CricketPath);
            if(line==null)throw new FileNotFoundException("Missing Figma line SVG",LinePath);

            GameObject[] regions={BuildSurface(),BuildOrnateBorder(),BuildHeader(line),BuildPortraitArea(cricket),BuildDescription(),BuildStatsTable(),BuildActions()};
            BuildPage(regions);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); Validate();
            Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(PagePath);
            Debug.Log("Rebuilt Figma 10:527: CricketDetail is composed from editable nested component prefabs.");
        }

        [MenuItem("Tools/Cricket UI/Validate Ququ Detail Prefabs")]
        public static void Validate()
        {
            GameObject page=AssetDatabase.LoadAssetAtPath<GameObject>(PagePath);
            if(page==null)throw new InvalidOperationException("Missing CricketDetail prefab.");
            if(page.GetComponent<Canvas>()==null)throw new InvalidOperationException("CricketDetail root must have a Canvas.");
            if(page.GetComponent<QuquXiangqingView>()==null)throw new InvalidOperationException("CricketDetail root must have QuquXiangqingView.");
            if(page.transform.childCount!=7)throw new InvalidOperationException("CricketDetail must have seven top-level regions.");
            foreach(string dependency in AssetDatabase.GetDependencies(PagePath,true))
                if(string.Equals(dependency,ReferenceExport,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("CricketDetail must not depend on the full-page PNG.");
            string[] buttons={Parts+"/btn-售卖.prefab",Parts+"/btn-收入背包.prefab",Parts+"/btn-关闭.prefab"};
            foreach(string button in buttons)
                if(AssetDatabase.LoadAssetAtPath<GameObject>(button)==null)
                    throw new InvalidOperationException("Missing action button prefab: "+button);
            GameObject statCard=AssetDatabase.LoadAssetAtPath<GameObject>(Parts+"/StatCard.prefab");
            if(statCard==null)throw new InvalidOperationException("Missing generic StatCard prefab.");
            if(statCard.transform.Find("Label")==null||statCard.transform.Find("Value")==null)
                throw new InvalidOperationException("StatCard must contain plain Label and Value children.");
            foreach(Transform child in statCard.transform)
                if(PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject))
                    throw new InvalidOperationException("StatCard children must be plain objects, not nested prefabs: "+child.name);
            Debug.Log("Ququ detail validation passed: generic StatCard plus action button prefabs.");
        }

        static GameObject BuildSurface()
        {
            GameObject go=Rect("details-popup-card",Design,Vector2.zero);
            BorderedPanel(go,Paper,PaperBorder,12,false).raycastTarget=false;
            return SavePart(go,"PageSurface.prefab");
        }

        static GameObject BuildOrnateBorder()
        {
            GameObject inner=Rect("Rectangle",new Vector2(964,1328),Vector2.zero);
            BorderLines(inner,new Vector2(964,1328),Gold,1);
            GameObject innerPrefab=SavePart(inner,"OrnateInnerFrame.prefab");
            GameObject outer=Rect("ornate-border",Design,Vector2.zero);
            BorderLines(outer,Design,Gold,2);
            Nest(outer,innerPrefab,Vector2.zero);
            return SavePart(outer,"OrnateBorder.prefab");
        }

        static GameObject BuildHeader(Sprite lineSprite)
        {
            GameObject title=SavePart(TextNode("◇ 促织 ◇","◇ 促织 ◇",new Vector2(117,49),32,LightGold,true),"TitleText.prefab");
            GameObject tab=Rect("traditional-tab",new Vector2(181,65),Vector2.zero);
            BorderedPanel(tab,Red,Gold,2,true).raycastTarget=false;
            Nest(tab,title,Inside(32,8,117,49,181,65));
            GameObject tabPrefab=SavePart(tab,"TraditionalTab.prefab");
            GameObject line=Rect("Line",new Vector2(500,4),Vector2.zero);
            SVGImage lineImage=line.AddComponent<SVGImage>(); lineImage.sprite=lineSprite; lineImage.preserveAspect=false; lineImage.raycastTarget=false;
            GameObject linePrefab=SavePart(line,"HeaderLine.prefab");
            GameObject header=Rect("popup-header",new Vector2(876,77),At(48,48,876,77));
            Nest(header,tabPrefab,Inside(347.5f,0,181,65,876,77)); Nest(header,linePrefab,Inside(188,75,500,4,876,77));
            return SavePart(header,"PopupHeader.prefab");
        }

        static GameObject BuildPortraitArea(Sprite cricket)
        {
            GameObject portrait=Rect("violet-cricket-illustration",new Vector2(260,260),Vector2.zero);
            Image portraitImage=portrait.AddComponent<Image>(); portraitImage.sprite=cricket; portraitImage.color=Color.white;
            portraitImage.preserveAspect=false; portraitImage.raycastTarget=true;
            GameObject portraitPrefab=SavePart(portrait,"Portrait.prefab");
            GameObject nameText=SavePart(TextNode("正紫龟丞相","正紫龟丞相",new Vector2(200,61),40,Red,true),"NameText.prefab");
            GameObject tag=Rect("name-tag",new Vector2(208,65),Vector2.zero);
            BorderedPanel(tag,Paper,Gold,2,false).raycastTarget=false;
            Nest(tag,nameText,Inside(8,4,200,61,208,65));
            GameObject tagPrefab=SavePart(tag,"NameTag.prefab");
            GameObject rank=SavePart(TextNode("领军将 (三品)","领军将 (三品)",new Vector2(208,55),36,Gold,true),"RankText.prefab");
            GameObject area=Rect("insect-portrait-area",new Vector2(876,390),At(48,149,876,390));
            Nest(area,portraitPrefab,Inside(308,0,260,260,876,390));
            Nest(area,tagPrefab,Inside(334,276,208,65,876,390));
            Nest(area,rank,Inside(342,352,208,55,876,390));
            return SavePart(area,"InsectPortraitArea.prefab");
        }

        static GameObject BuildDescription()
        {
            GameObject copy=TextNode("古典描述","龟形鹤项虾脊梁，头如蚕嘴肚如琴\n识者若逢此促织，这般号作大将军",new Vector2(844,116),32,Ink,false);
            copy.GetComponent<TextMeshProUGUI>().lineSpacing=12;
            GameObject copyPrefab=SavePart(copy,"DescriptionText.prefab");
            GameObject group=Rect("classical-description",new Vector2(876,72),At(48,563,876,72));
            Nest(group,copyPrefab,Inside(16,0,844,116,876,72));
            return SavePart(group,"ClassicalDescription.prefab");
        }

        static GameObject BuildStatsTable()
        {
            GameObject template=EnsureStatCardPrefab();
            GameObject table=Rect("stats-table",new Vector2(876,279),At(56,715,876,279));
            float[] tops={12,67,122,177,232};
            int rowCount=Stats.GetLength(0);
            for(int row=0;row<rowCount;row++)
            {
                GameObject rowObject=Rect("Frame-Row"+(row+1),new Vector2(876,47),Vector2.zero);
                for(int col=0;col<2;col++)
                {
                    GameObject card=Nest(rowObject,template,Inside(col==0?0:446,0,430,47,876,47));
                    ApplyStatCard(card,Stats[row,col]);
                }
                GameObject rowPrefab=SavePart(rowObject,"StatsRow"+(row+1).ToString("00")+".prefab");
                Nest(table,rowPrefab,Inside(0,tops[row],876,47,876,279));
            }
            return SavePart(table,"StatsTable.prefab");
        }

        static GameObject EnsureStatCardPrefab()
        {
            if(statCardPrefab!=null)return statCardPrefab;
            GameObject card=Rect("StatCard",new Vector2(430,47),Vector2.zero);
            RoundedImage(card,CardWhite,true).raycastTarget=true;
            GameObject label=TextNode("Label","属性",new Vector2(200,27),18,Muted,true);
            label.GetComponent<TextMeshProUGUI>().alignment=TextAlignmentOptions.MidlineLeft;
            label.transform.SetParent(card.transform,false);
            label.GetComponent<RectTransform>().anchoredPosition=Inside(16,10,200,27,430,47);
            GameObject value=TextNode("Value","—",new Vector2(96,22),18,Green,true);
            value.GetComponent<TextMeshProUGUI>().alignment=TextAlignmentOptions.MidlineRight;
            value.transform.SetParent(card.transform,false);
            value.GetComponent<RectTransform>().anchoredPosition=Inside(318,12.5f,96,22,430,47);
            statCardPrefab=SavePart(card,"StatCard.prefab");
            return statCardPrefab;
        }

        static void ApplyStatCard(GameObject card,Stat stat)
        {
            card.name="Frame-"+stat.label;
            Transform label=card.transform.Find("Label");
            Transform value=card.transform.Find("Value");
            if(label==null||value==null)throw new InvalidOperationException("StatCard missing Label/Value children.");
            label.GetComponent<TextMeshProUGUI>().text=stat.label;
            value.GetComponent<TextMeshProUGUI>().text=stat.value;
        }

        static GameObject BuildActions()
        {
            GameObject sell=ActionButton("btn-售卖 236","售卖 236",Brown,"btn-售卖.prefab","btn-售卖_Label.prefab");
            GameObject store=ActionButton("btn-确定","收入背包",Red,"btn-收入背包.prefab","btn-收入背包_Label.prefab");
            GameObject close=ActionButton("btn-售卖 237","关闭",Brown,"btn-关闭.prefab","btn-关闭_Label.prefab");
            GameObject stack=Rect("action-button-stack",new Vector2(876,260),At(48,1028,876,260));
            Nest(stack,sell,Inside(198,12,480,72,876,260)); Nest(stack,store,Inside(198,100,480,72,876,260));
            Nest(stack,close,Inside(198,188,480,72,876,260));
            return SavePart(stack,"ActionButtonStack.prefab");
        }

        static GameObject ActionButton(string figmaName,string caption,Color fill,string buttonFile,string labelFile)
        {
            GameObject label=SavePart(TextNode(caption,caption,new Vector2(460,60),28,LightGold,true),labelFile);
            GameObject go=Rect(figmaName,new Vector2(480,72),Vector2.zero);
            Image image=BorderedPanel(go,fill,Gold,2,true); image.raycastTarget=true;
            Shadow shadow=go.AddComponent<Shadow>(); shadow.effectColor=new Color(0,0,0,.25f);
            shadow.effectDistance=new Vector2(0,-4); shadow.useGraphicAlpha=true;
            Button button=go.AddComponent<Button>(); button.targetGraphic=image; button.navigation=new Navigation{mode=Navigation.Mode.None};
            Nest(go,label,Vector2.zero);
            return SavePart(go,buttonFile);
        }

        static void BuildPage(GameObject[] regions)
        {
            GameObject go=Rect("CricketDetail",Design,Vector2.zero);
            Canvas canvas=go.AddComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler=go.AddComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=Design; scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight=0;
            go.AddComponent<GraphicRaycaster>();
            RectTransform rect=go.GetComponent<RectTransform>(); rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one;
            rect.offsetMin=Vector2.zero; rect.offsetMax=Vector2.zero;
            foreach(GameObject region in regions)Nest(go,region,region.GetComponent<RectTransform>().anchoredPosition);
            QuquXiangqingView view=go.AddComponent<QuquXiangqingView>();
            view.sellButton=FindButton(go,"btn-售卖");
            view.storeButton=FindButton(go,"btn-收入背包");
            view.closeButton=FindButton(go,"btn-关闭");
            Save(go,PagePath);
        }

        static void BuildScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            GameObject page=PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PagePath)) as GameObject;
            if(page==null)throw new InvalidOperationException("Failed to instantiate page.");
            SceneManager.MoveGameObjectToScene(page,SceneManager.GetActiveScene());
            GameObject cameraObject=new GameObject("Main Camera"); Camera camera=cameraObject.AddComponent<Camera>();
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.08f,.06f,.04f);
            camera.transform.position=new Vector3(0,0,-10); cameraObject.tag="MainCamera";
            GameObject lightObject=new GameObject("Directional Light"); Light light=lightObject.AddComponent<Light>();
            light.type=LightType.Directional; light.intensity=1; light.transform.rotation=Quaternion.Euler(50,-30,0);
            GameObject eventSystem=new GameObject("EventSystem"); eventSystem.AddComponent<EventSystem>(); eventSystem.AddComponent<StandaloneInputModule>();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ScenePath);
        }

        static GameObject TextNode(string name,string value,Vector2 size,float fontSize,Color color,bool bold)
        {
            GameObject go=Rect(name,size,Vector2.zero); TextMeshProUGUI text=go.AddComponent<TextMeshProUGUI>();
            text.text=value; text.font=font; text.fontSize=fontSize; text.color=color;
            text.fontStyle=bold?FontStyles.Bold:FontStyles.Normal; text.alignment=TextAlignmentOptions.Center;
            text.enableWordWrapping=false; text.overflowMode=TextOverflowModes.Overflow; text.raycastTarget=false;
            return go;
        }

        static Image RoundedImage(GameObject go,Color color,bool sliced)
        {
            Image image=go.AddComponent<Image>(); image.sprite=rounded; image.color=color;
            image.type=sliced&&rounded!=null?Image.Type.Sliced:Image.Type.Simple; return image;
        }

        static Image PlainImage(GameObject go,Color color)
        {
            Image image=go.AddComponent<Image>(); image.sprite=null; image.type=Image.Type.Simple; image.color=color; return image;
        }

        static Image BorderedPanel(GameObject go,Color fill,Color border,float width,bool roundedCorners)
        {
            Image outer=roundedCorners?RoundedImage(go,border,true):PlainImage(go,border);
            Vector2 size=go.GetComponent<RectTransform>().sizeDelta-new Vector2(width*2,width*2);
            GameObject inner=Rect("Fill",size,Vector2.zero); inner.transform.SetParent(go.transform,false);
            Image innerImage=roundedCorners?RoundedImage(inner,fill,true):PlainImage(inner,fill);
            innerImage.raycastTarget=false; return outer;
        }

        static void BorderLines(GameObject go,Vector2 size,Color color,float width)
        {
            AddEdge(go,"Top",new Vector2(size.x,width),new Vector2(0,size.y*.5f-width*.5f),color);
            AddEdge(go,"Bottom",new Vector2(size.x,width),new Vector2(0,-size.y*.5f+width*.5f),color);
            AddEdge(go,"Left",new Vector2(width,size.y),new Vector2(-size.x*.5f+width*.5f,0),color);
            AddEdge(go,"Right",new Vector2(width,size.y),new Vector2(size.x*.5f-width*.5f,0),color);
        }

        static void AddEdge(GameObject parent,string name,Vector2 size,Vector2 position,Color color)
        {
            GameObject edge=Rect(name,size,position); edge.transform.SetParent(parent.transform,false);
            PlainImage(edge,color).raycastTarget=false;
        }

        static UnityEngine.UI.Outline AddOutline(GameObject go,Color color,float width)
        {
            UnityEngine.UI.Outline outline=go.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor=color; outline.effectDistance=new Vector2(width,width); outline.useGraphicAlpha=true; return outline;
        }

        static GameObject Rect(string name,Vector2 size,Vector2 position)
        {
            GameObject go=new GameObject(name); RectTransform rect=go.AddComponent<RectTransform>();
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f); rect.sizeDelta=size; rect.anchoredPosition=position; return go;
        }

        static Vector2 At(float x,float y,float w,float h){return Inside(x,y,w,h,Design.x,Design.y);}
        static Vector2 Inside(float x,float y,float w,float h,float pw,float ph){return new Vector2(x+w*.5f-pw*.5f,ph*.5f-y-h*.5f);}

        static GameObject Nest(GameObject parent,GameObject prefab,Vector2 position)
        {
            GameObject instance=PrefabUtility.InstantiatePrefab(prefab,parent.transform) as GameObject;
            if(instance==null)throw new InvalidOperationException("Could not nest prefab "+prefab.name);
            instance.GetComponent<RectTransform>().anchoredPosition=position; return instance;
        }

        static GameObject SavePart(GameObject go,string file){return Save(go,Parts+"/"+file);}
        static GameObject Save(GameObject go,string path)
        {
            GameObject saved=PrefabUtility.SaveAsPrefabAsset(go,path); UnityEngine.Object.DestroyImmediate(go);
            if(saved==null)throw new InvalidOperationException("Could not save "+path);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static Button FindButton(GameObject root,string name)
        {
            foreach(Button button in root.GetComponentsInChildren<Button>(true))if(button.gameObject.name==name)return button;
            throw new InvalidOperationException("Missing button "+name);
        }

        static Sprite PrepareSprite(string path)
        {
            TextureImporter importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer!=null){importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();}
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void DeleteLegacy()
        {
            AssetDatabase.DeleteAsset(PagePath);
            AssetDatabase.DeleteAsset(Prefabs+"/详情页.prefab");
            AssetDatabase.DeleteAsset(Prefabs+"/Canvas.prefab");
            string[] obsolete={Parts+"/蛐蛐详情页_Background.prefab",Parts+"/蛐蛐详情页_Header.prefab",
                Parts+"/蛐蛐详情页_InteractionOverlay.prefab",Parts+"/蛐蛐详情页_portrait-hit.prefab",Parts+"/蛐蛐详情页_stats-hit.prefab"};
            foreach(string path in obsolete)AssetDatabase.DeleteAsset(path);
            foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{Parts}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                string file=Path.GetFileName(path);
                if(file.StartsWith("StatsRow",StringComparison.Ordinal)
                    ||(file.StartsWith("StatCard",StringComparison.Ordinal)&&file!="StatCard.prefab")
                    ||(file.StartsWith("Stat",StringComparison.Ordinal)&&(file.Contains("_Label")||file.Contains("_Value"))))
                    AssetDatabase.DeleteAsset(path);
            }
        }

        static void EnsureFolders()
        {
            EnsureFolder("Assets/Game");EnsureFolder("Assets/Game/UI");EnsureFolder(Root);EnsureFolder(Prefabs);
            EnsureFolder(Parts);EnsureFolder(Textures);EnsureFolder("Assets/Scenes");
        }

        static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            string parent=Path.GetDirectoryName(path).Replace("\\","/");
            if(!AssetDatabase.IsValidFolder(parent))EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }

        static void AppendBuildSettings(string path)
        {
            List<EditorBuildSettingsScene> scenes=new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach(EditorBuildSettingsScene scene in scenes)if(scene.path==path)return;
            scenes.Add(new EditorBuildSettingsScene(path,true));EditorBuildSettings.scenes=scenes.ToArray();
        }
    }
}