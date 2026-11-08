using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace RuinRelay.LandscapeKernel
{
    public sealed class LandscapePreviewWindow:EditorWindow
    {
        Terrain terrain;PlanSettings settings=new PlanSettings();PreviewCamera cameraPose=new PreviewCamera();PreviewFrame frame;
        ObjectField terrainField;DropdownField mode,view;TextField intent;Vector2Field start,end;FloatField half,blend,cap,amplitude,minimum;Slider influence;
        Vector3Field cameraPosition,cameraTarget;Toggle live;Image image;Label summary,status;double due=-1;bool updating,stale=true;
        [MenuItem("Tools/Landscape Kernel/Live Preview")]
        public static void Open(){GetWindow<LandscapePreviewWindow>("Landscape Preview").Show();}
        public static LandscapePreviewWindow Configure(Terrain t,PlanSettings p,PreviewCamera c)
        {
            var w=GetWindow<LandscapePreviewWindow>("Landscape Preview");w.terrain=t;w.settings=p;w.cameraPose=c;w.CreateGUI();w.Show();return w;
        }
        void OnEnable(){minSize=new Vector2(720,680);EditorApplication.update+=Tick;}
        void OnDisable(){EditorApplication.update-=Tick;frame?.Dispose();frame=null;}
        public void CreateGUI()
        {
            var root=rootVisualElement;root.Clear();var style=AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.ruinrelay.landscape-kernel/Editor/Preview.uss");if(style&&!root.styleSheets.Contains(style))root.styleSheets.Add(style);
            var scroll=new ScrollView();root.Add(scroll);
            scroll.Add(new Label("LANDSCAPE · LIVE PREVIEW"){name="title"});
            scroll.Add(new HelpBox("Nur Vorschau. Die Spielwelt wird nicht gespeichert oder dauerhaft verändert. Wirkung ist keine Art-Freigabe.",HelpBoxMessageType.Info));
            terrainField=new ObjectField("Terrain"){objectType=typeof(Terrain),allowSceneObjects=true,value=terrain};scroll.Add(terrainField);terrainField.RegisterValueChangedCallback(e=>{terrain=(Terrain)e.newValue;Schedule();});
            intent=new TextField("Sichtbares Ziel"){value=settings.intent};scroll.Add(intent);intent.RegisterValueChangedCallback(e=>{settings.intent=e.newValue;Schedule();});
            mode=new DropdownField("Verfahren",new System.Collections.Generic.List<string>{"corridor","ridge"},settings.mode=="ridge"?1:0);scroll.Add(mode);mode.RegisterValueChangedCallback(e=>{settings.mode=e.newValue;Schedule();});
            start=new Vector2Field("Start · Welt X/Z"){value=settings.start};end=new Vector2Field("Ende · Welt X/Z"){value=settings.end};scroll.Add(start);scroll.Add(end);
            start.RegisterValueChangedCallback(e=>{settings.start=e.newValue;Schedule();});end.RegisterValueChangedCallback(e=>{settings.end=e.newValue;Schedule();});
            half=Number(scroll,"Halbe Kernbreite · m",settings.halfWidth,v=>settings.halfWidth=v);blend=Number(scroll,"Randübergang · m",settings.blend,v=>settings.blend=v);
            amplitude=Number(scroll,"Rückenhöhe / Senke · m",settings.amplitude,v=>settings.amplitude=v);cap=Number(scroll,"Änderungsgrenze · m",settings.maxChange,v=>settings.maxChange=v);
            minimum=Number(scroll,"Zielbezogene Wirkungsschwelle · m",settings.minimumUsefulChange,v=>settings.minimumUsefulChange=v);
            influence=new Slider("Einflussgewicht",0,1){value=settings.influence,showInputField=true,name="influence"};scroll.Add(influence);influence.RegisterValueChangedCallback(e=>{settings.influence=e.newValue;Schedule();});
            cameraPosition=new Vector3Field("Feste Kamera"){value=cameraPose.position};cameraTarget=new Vector3Field("Blickziel"){value=cameraPose.target};scroll.Add(cameraPosition);scroll.Add(cameraTarget);
            cameraPosition.RegisterValueChangedCallback(e=>{cameraPose.position=e.newValue;Schedule();});cameraTarget.RegisterValueChangedCallback(e=>{cameraPose.target=e.newValue;Schedule();});
            var buttons=new VisualElement();buttons.AddToClassList("row");scroll.Add(buttons);
            buttons.Add(new Button(()=>{if(SceneView.lastActiveSceneView){cameraPose.position=SceneView.lastActiveSceneView.camera.transform.position;cameraPose.target=cameraPose.position+SceneView.lastActiveSceneView.camera.transform.forward*80;cameraPosition.SetValueWithoutNotify(cameraPose.position);cameraTarget.SetValueWithoutNotify(cameraPose.target);Schedule();}}){text="Scene-Kamera übernehmen"});
            buttons.Add(new Button(()=>EvaluateNow()){text="Vorschau berechnen",name="evaluate"});buttons.Add(new Button(Export){text="Bilder + Daten exportieren"});
            live=new Toggle("Nach Parameteränderung automatisch aktualisieren (500 ms)"){value=false};scroll.Add(live);
            live.RegisterValueChangedCallback(e=>{if(e.newValue)Schedule();else due=-1;});
            view=new DropdownField("Ansicht",new System.Collections.Generic.List<string>{"Vorher","Vorschau","Höhenänderung"},1);scroll.Add(view);view.RegisterValueChangedCallback(e=>ShowImage());
            summary=new Label(){name="metrics"};summary.AddToClassList("wrapped");scroll.Add(summary);status=new Label("Noch keine Vorschau berechnet."){name="status"};status.AddToClassList("wrapped");scroll.Add(status);
            image=new Image(){name="comparison",scaleMode=ScaleMode.ScaleToFit};scroll.Add(image);
        }
        FloatField Number(VisualElement root,string label,float value,Action<float> assign){var f=new FloatField(label){value=value};root.Add(f);f.RegisterValueChangedCallback(e=>{assign(e.newValue);Schedule();});return f;}
        void Schedule(){stale=true;if(status!=null)status.text="Parameter geändert; alte Bilder sind nicht mehr aktuell.";if(amplitude!=null)amplitude.SetEnabled(settings.mode=="ridge");if(live!=null&&live.value)due=EditorApplication.timeSinceStartup+.5;}
        void Tick(){if(due>=0&&EditorApplication.timeSinceStartup>=due){due=-1;EvaluateNow();}}
        public void EvaluateNow()
        {
            if(updating)return;updating=true;
            try{var next=PreviewEngine.Render(terrain,settings,cameraPose);frame?.Dispose();frame=next;stale=false;var m=frame.plan.Metrics;
                summary.text=$"Max. {frame.persistedPreviewMaxMetres:F3} m · Mittel {m.meanAbsMetres:F3} m · Fläche ≈ {m.changedAreaM2:F0} m² · {m.changedSamples} Samples · Berechnung + Bilder {frame.milliseconds:F0} ms";
                status.text=m.assessment+"  Blau: Absenkung, Orange: Anhebung, Grün: geschützt. Auflage und Hindernisse nicht geprüft. Heatmap relativ zum Maximum; kein Qualitätsmaß.";ShowImage();}
            catch(Exception e){status.text="Vorschau fehlgeschlagen: "+e.Message;}
            finally{updating=false;}
        }
        void ShowImage(){if(frame==null||image==null)return;image.image=view.value=="Vorher"?frame.before:view.value=="Höhenänderung"?frame.delta:frame.after;}
        void Export(){if(frame==null||stale){status.text="Vor Export eine aktuelle Vorschau berechnen.";return;}try{frame.plan.Validate(terrain);}catch(Exception e){stale=true;status.text="Vorschau veraltet: "+e.Message;return;}string path=EditorUtility.OpenFolderPanel("Vorschau exportieren","","");if(!string.IsNullOrEmpty(path))frame.Export(path,settings,cameraPose);}
        public object State()=>new{hasFrame=frame!=null,imageWidth=frame?.after?.width??0,metrics=frame?.plan.Metrics,status=status?.text,elementCount=rootVisualElement.Query().ToList().Count};
    }
}

