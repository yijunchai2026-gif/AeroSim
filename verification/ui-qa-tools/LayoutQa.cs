using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AeroSim;
using AeroSim.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

namespace AeroSim.Qa
{
    public static class QaBootstrap
    {
        private static bool _started;
        public static void Initialize()
        {
            if(_started||!Environment.GetCommandLineArgs().Contains("--layout-qa"))return;
            _started=true;var host=new GameObject("Isolated Layout QA");UnityEngine.Object.DontDestroyOnLoad(host);host.AddComponent<LayoutQa>();
        }
    }

    [Serializable] public class QaCheck {public string rule,target,evidence;public bool passed;}
    [Serializable] public class QaElement {public string type,name,text,classes;public Rect bounds;public float font;}
    [Serializable] public class QaCase
    {
        public string state,screenshot;public int width,height;public float scale,textMultiplier,logicalWidth,logicalHeight;
        public List<QaCheck> checks=new List<QaCheck>();public List<QaElement> elements=new List<QaElement>();
    }
    [Serializable] public class QaReport
    {
        public string startedUtc=DateTime.UtcNow.ToString("O"),version;
        public string application="AeroSim 0.4 UI candidate COPY",mode="Native persisted UI/HUD/Text settings; DPI cases are logical density probes, not physical iOS tests";
        public int width,height,runtimeErrors;public List<QaCase> cases=new List<QaCase>();
    }
    public sealed class LayoutQa : MonoBehaviour
    {
        private AeroSimApplication _app;private UIDocument _document;private VisualElement _root;private Mouse _mouse;
        private QaReport _report=new QaReport();private QaCase _case;private string _dir;private int _runtimeErrors;
        private SessionRecord _fixture;
        private static string Arg(string key,string fallback=null){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0&&i<a.Length-1?a[i+1]:fallback;}
        private void OnEnable()=>Application.logMessageReceived+=Track;
        private void OnDisable()=>Application.logMessageReceived-=Track;
        private void Track(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)_runtimeErrors++;}
        private void Check(string rule,bool result,string target,string evidence="")
        {
            _case.checks.Add(new QaCheck{rule=rule,passed=result,target=target,evidence=evidence});
        }
        private static bool Visible(VisualElement e)
        {
            for(var n=e;n!=null;n=n.parent)if(n.resolvedStyle.display==DisplayStyle.None||n.resolvedStyle.visibility==Visibility.Hidden)return false;
            return e.worldBound.width>0&&e.worldBound.height>0;
        }
        private static string Target(VisualElement e)=>!string.IsNullOrEmpty(e.name)?e.name:e is TextElement t?t.text:e.GetType().Name;
        private static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
        private static bool Inside(Rect a,Rect b,float tolerance=2)=>a.xMin>=b.xMin-tolerance&&a.xMax<=b.xMax+tolerance&&a.yMin>=b.yMin-tolerance&&a.yMax<=b.yMax+tolerance;
        private Rect Viewport=>new Rect(0,0,_root.resolvedStyle.width,_root.resolvedStyle.height);
        private static bool GenuineOverlap(Rect a,Rect b)
        {
            return Mathf.Min(a.xMax,b.xMax)-Mathf.Max(a.xMin,b.xMin)>3&&Mathf.Min(a.yMax,b.yMax)-Mathf.Max(a.yMin,b.yMin)>3;
        }
        private IEnumerator Settle(){yield return new WaitForSecondsRealtime(.08f);yield return null;yield return null;}
        private IEnumerator Start()
        {
            _dir=Arg("--layout-qa");Directory.CreateDirectory(_dir);
            if(Arg("--data-dir")==null){File.WriteAllText(Path.Combine(_dir,"ERROR.txt"),"Isolated --data-dir is mandatory.");Application.Quit(2);yield break;}
            yield return Settle();
            _app=UnityEngine.Object.FindFirstObjectByType<AeroSimApplication>();_document=_app.GetComponent<UIDocument>();_root=_document.rootVisualElement;
            _report.width=Screen.width;_report.height=Screen.height;_report.version=Application.version;
            LocalStore.Profile.onboarded=true;LocalStore.Profile.selectedAircraft="mini4";LocalStore.Save();
            _fixture=new SessionRecord{id="ui-qa-fixture",startedUtc="2026-10-05T00:00:00Z",title="QA fixture — not a pilot achievement",workspace="Aerial",environment="Pacific Coast",aircraft="DJI Mavic 3 Pro",duration=20,score=84,feedback="Synthetic layout test fixture only."};
            _fixture.config=new FlightConfig{aircraft="DJI Mavic 3 Pro",title=_fixture.title};
            _fixture.frames.Add(new TelemetryFrame{time=0,position=new Vector3(0,8,10),battery=100});
            _fixture.frames.Add(new TelemetryFrame{time=20,position=new Vector3(10,12,25),battery=99});
            LocalStore.SaveSession(_fixture);
            yield return PreferencesRegression();
            var scales=Arg("--qa-scales","0.8,0.9,1,1.1,1.25,1.5").Split(',').Select(s=>float.Parse(s,CultureInfo.InvariantCulture)).ToArray();
            foreach(float scale in scales)yield return Matrix(scale,1);
            if(Arg("--qa-text-stress")=="true")yield return Matrix(1,2);
            _app.ShowPage(Page.Home);yield return Settle();
            _report.runtimeErrors=_runtimeErrors;
            File.WriteAllText(Path.Combine(_dir,"report.json"),JsonUtility.ToJson(_report,true));
            int failures=_report.cases.Sum(c=>c.checks.Count(x=>!x.passed));
            File.WriteAllText(Path.Combine(_dir,"summary.txt"),"States: "+_report.cases.Count+"\nChecks: "+_report.cases.Sum(c=>c.checks.Count)+"\nFailed checks: "+failures+"\nRuntime errors: "+_runtimeErrors+"\n");
            if(_mouse!=null)InputSystem.RemoveDevice(_mouse);
            Debug.Log("Layout QA completed: "+_report.cases.Count+" states; "+failures+" failed checks; "+_runtimeErrors+" runtime errors.");
            Application.Quit(_runtimeErrors==0?0:2);
        }
        private IEnumerator Matrix(float scale,float text)
        {
            LocalStore.Profile.settings.uiScale=(scale*100).ToString("0",CultureInfo.InvariantCulture)+"%";
            LocalStore.Profile.settings.hudScale=LocalStore.Profile.settings.uiScale;
            LocalStore.Profile.settings.textScale=text;
            _app.RefreshUiPreferences();
            foreach(var workspace in new[]{"Aerial","FPV","Professional"})
            {
                _app.ChangeWorkspace(workspace);yield return Settle();
                var pages=workspace=="Aerial"?new[]{Page.Onboarding,Page.Home,Page.Controller,Page.Academy,Page.Brief,Page.Preflight,Page.FreeFlight,Page.Challenges,Page.Hangar,Page.Settings,Page.Readiness,Page.Records,Page.Results,Page.Report}:
                    workspace=="FPV"?new[]{Page.Home,Page.Academy,Page.Challenges,Page.Tuning,Page.OsdEditor}:
                    new[]{Page.Home,Page.Catalog,Page.Brief,Page.Preflight,Page.MissionEditor,Page.Instructor};
                foreach(var page in pages)
                {
                    _app.PreparePreview(page);yield return Settle();yield return Inspect(workspace+"-"+page,scale,text);
                }
                if(workspace=="Aerial")
                {
                    _app.ShowPage(Page.FreeFlight);yield return Settle();
                    BeginCase("Aerial-LaunchInteraction",scale,text);
                    yield return Click("Launch Free Flight");
                    Check("interaction.launch",_app.CurrentPage==Page.Flight,"Launch Free Flight","Real Input System pointer event at scaled coordinates");
                    if(_app.CurrentPage!=Page.Flight)_app.StartFlight(new FlightConfig());
                }
                else _app.StartFlight(new FlightConfig{workspace=workspace,aircraft=workspace=="FPV"?"DJI Avata 2":"DJI Mavic 3 Pro",professional=workspace=="Professional",mission=workspace=="Professional"?new MissionDefinition():null});
                _app.Simulator.Body.position=new Vector3(0,8,10);_app.Simulator.Pause(true);_app.Simulator.Body.transform.position=_app.Simulator.Body.position;
                yield return Settle();yield return Inspect(workspace+"-Flight",scale,text);
                if(workspace=="Aerial")
                {
                    _app.Simulator.Pause(false);yield return Click("Pause");yield return Settle();yield return Inspect("Aerial-Pause",scale,text);
                    Check("interaction.pause",_app.Simulator.Paused,"Pause");
                    yield return Click("Resume Flight");Check("interaction.resume",!_app.Simulator.Paused,"Resume Flight");
                    _app.BeginReplay(_fixture);_app.Simulator.Seek(10);yield return Settle();yield return Inspect("Aerial-Replay",scale,text);
                }
            }
        }
        private IEnumerator PreferencesRegression()
        {
            _app.ShowPage(Page.Settings); yield return Settle();
            BeginCase("Native-Preferences",1,1);
            _root.Query<DropdownField>().ToList().First(f=>f.label=="UI Scale").value="125%";
            _root.Query<DropdownField>().ToList().First(f=>f.label=="HUD Scale").value="80%";
            _root.Query<DropdownField>().ToList().First(f=>f.label=="Text Size").value="200%";
            yield return Settle();
            Check("regression.ui-scale-immediate",Mathf.Abs(_document.panelSettings.scale-1.25f)<.01f,"UI setting callback");
            LocalStore.Initialize();
            Check("regression.scales-persist",LocalStore.Profile.settings.uiScale=="125%"&&LocalStore.Profile.settings.hudScale=="80%"&&LocalStore.Profile.settings.textScale==2,"Persisted profile reload");
            _app.StartFlight(new FlightConfig());yield return Settle();
            Check("regression.hud-scale-independent",Mathf.Abs(_document.panelSettings.scale-.8f)<.01f,"Flight uses HUD preference");
            _app.ShowPage(Page.Home);yield return Settle();
            Check("regression.menu-scale-restored",Mathf.Abs(_document.panelSettings.scale-1.25f)<.01f,"Return from flight");
            LocalStore.Profile.settings.uiScale="100%";LocalStore.Profile.settings.hudScale="100%";LocalStore.Profile.settings.textScale=1;
            LocalStore.Save();_app.RefreshUiPreferences();yield return Settle();
        }
        private void BeginCase(string state,float scale,float text)
        {
            _case=new QaCase{state=state,width=Screen.width,height=Screen.height,scale=scale,textMultiplier=text,
                logicalWidth=_root.resolvedStyle.width,logicalHeight=_root.resolvedStyle.height};
            _report.cases.Add(_case);
        }
        private IEnumerator Inspect(string state,float scale,float text)
        {
            yield return Settle();
            BeginCase(state,scale,text);
            Check("viewport.panel-scale",Mathf.Abs(Viewport.width-Screen.width/scale)<2&&Mathf.Abs(Viewport.height-Screen.height/scale)<2,"Root",Viewport.ToString());
            var elements=_root.Query<VisualElement>().ToList();
            foreach(var element in elements)
            {
                if(!Visible(element))continue;
                if(element is TextElement te)
                {
                    if(string.IsNullOrWhiteSpace(te.text))continue;
                    _case.elements.Add(new QaElement{type=element.GetType().Name,name=element.name,text=te.text,classes=string.Join(" ",element.GetClasses()),bounds=element.worldBound,font=element.resolvedStyle.fontSize});
                    if(element is Label||element is Button)
                    {
                        float font=element.resolvedStyle.fontSize;
                        Check("design.minimum-font-12",font>=11.95f,Target(element),font.ToString("F1"));
                        float w=element.contentRect.width,h=element.contentRect.height;
                        if(w>2&&h>2&&font>0&&Inside(element.worldBound,Viewport))
                        {
                            var required=te.MeasureTextSize(te.text,w,element.resolvedStyle.whiteSpace==WhiteSpace.Normal?VisualElement.MeasureMode.Exactly:VisualElement.MeasureMode.Undefined,0,VisualElement.MeasureMode.Undefined);
                            bool clipped=required.y>h+3||(element.resolvedStyle.whiteSpace!=WhiteSpace.Normal&&required.x>w+3);
                            Check("layout.text-fits",!clipped,Target(element),"available="+w.ToString("F1")+"x"+h.ToString("F1")+" measured="+required);
                        }
                    }
                }
                if(element.ClassListContains("product-panel")||element.ClassListContains("product-field"))
                {
                    var scroll=element.GetFirstAncestorOfType<ScrollView>();
                    var r=scroll!=null?scroll.contentViewport.worldBound:Viewport;
                    Check("layout.horizontal-containment",element.worldBound.xMin>=r.xMin-2&&element.worldBound.xMax<=r.xMax+2,Target(element),element.worldBound+" in "+r);
                }
                if(element is Button button&&(button.ClassListContains("primary-button")||button.ClassListContains("action-button")))
                    Check("design.action-height-44",button.worldBound.height>=44-1.05f/scale,Target(button),button.worldBound.ToString()+"; <=1 physical pixel rounding tolerance");
            }
            var buttons=_root.Query<Button>().ToList().Where(Visible).ToList();
            for(int i=0;i<buttons.Count;i++)for(int j=i+1;j<buttons.Count;j++)
                if(buttons[i].parent==buttons[j].parent)
                    Check("layout.sibling-buttons-no-overlap",!GenuineOverlap(buttons[i].worldBound,buttons[j].worldBound),Target(buttons[i])+" / "+Target(buttons[j]));
            if(state.EndsWith("-Settings"))
            {
                var labels=string.Join("\n",_root.Query<TextElement>().ToList().Select(e=>e.text));
                Check("design.user-ui-scale-setting",labels.Contains("UI Scale"),"Settings","PRD 18.4.3: 80/90/100/110/125/150% + Auto");
                Check("design.independent-hud-scale-setting",labels.Contains("HUD Scale"),"Settings","PRD 18.4.3");
                Check("design.text-200-user-setting",_root.Query<DropdownField>().ToList().Any(f=>f.choices.Contains("200%")),"Settings","PRD 18.16, native selectable text size");
            }
            var bar=_root.Q<VisualElement>(className:"top-bar");
            if(bar!=null)
            {
                Check("design.topbar-height-64",Mathf.Abs(bar.worldBound.height-64)<1,"Top bar",bar.worldBound.height.ToString("F1"));
                var nav=_root.Q<ScrollView>(className:"side-nav");
                float expected=Viewport.width<1280?72:240;
                if(nav!=null)Check("design.nav-width",Mathf.Abs(nav.worldBound.width-expected)<2,"Navigation","actual="+nav.worldBound.width+" expected="+expected);
                var main=_root.Q<VisualElement>(className:"main-content");
                if(main!=null&&Viewport.width>1920)Check("design.content-max-1600",main.worldBound.width<=1602,"Main content",main.worldBound.width.ToString("F1"));
            }
            if(state.EndsWith("-Hangar"))Check("regression.six-aircraft",elements.Count(e=>e.ClassListContains("aircraft-card"))==6,"Hangar");
            var osd=_root.Q<VisualElement>(className:"osd-preview");
            if(osd!=null) {
                var tokens=osd.Query<Label>(className:"osd-token").ToList();
                foreach(var token in tokens)Check("layout.osd-token-contained",Inside(token.worldBound,osd.worldBound),Target(token),token.worldBound+" in "+osd.worldBound);
                for(int i=0;i<tokens.Count;i++)for(int j=i+1;j<tokens.Count;j++)Check("layout.osd-tokens-no-overlap",!GenuineOverlap(tokens[i].worldBound,tokens[j].worldBound),Target(tokens[i])+" / "+Target(tokens[j]));
            }
            if(state.EndsWith("-Flight")||state=="Aerial-Replay")AuditHud();
            if(state=="Aerial-Pause")
            {
                var panel=_root.Q<VisualElement>(className:"modal-overlay").Q<VisualElement>(className:"product-panel");
                var modal=panel.GetFirstAncestorOfType<ScrollView>();
                Check("layout.pause-panel-contained",modal!=null&&Inside(modal.worldBound,Viewport),"Pause scroll viewport",modal?.worldBound.ToString());
                foreach(var b in panel.Query<Button>().ToList()) {
                    if(modal!=null){modal.ScrollTo(b);yield return Settle();}
                    Check("layout.pause-action-visible",Inside(b.worldBound,Viewport)&&Inside(b.worldBound,modal.contentViewport.worldBound),Target(b),b.worldBound.ToString());
                }
                if(modal!=null){modal.scrollOffset=Vector2.zero;yield return Settle();}
            }
            bool screenshot=(Screen.width==1920&&Mathf.Abs(scale-1)<.01f&&text==1)||
                ((Screen.width==1280&&(scale==.8f||scale==1||scale==1.5f))||Screen.width<1100||scale>=2||scale==1&&(Screen.width>=3800||Screen.height==1080&&Screen.width>2400)||text>1||Screen.width==1920&&scale==1.25f)&&
                new[]{"Aerial-Home","Aerial-FreeFlight","Aerial-Hangar","Aerial-Settings","Aerial-Flight","Aerial-Pause","Aerial-Replay","FPV-OsdEditor","Professional-MissionEditor"}.Contains(state);
            if(screenshot)
            {
                _case.screenshot=(scale*100).ToString("0")+"-text"+text.ToString("0")+"-"+state+".png";yield return Capture(Path.Combine(_dir,_case.screenshot));
            }
            // Check the first and last primary actions plus all aircraft actions after scrolling.
            var actions=buttons.Where(b=>b.enabledInHierarchy&&(b.ClassListContains("primary-button")||b.ClassListContains("action-button"))).ToList();
            var targets=actions.Take(1).Concat(actions.Skip(Mathf.Max(0,actions.Count-1))).Distinct().ToList();
            if(state.EndsWith("-Hangar"))targets=actions.Where(b=>b.text.StartsWith("Select ")||b.text.StartsWith("Fly Selected")).ToList();
            foreach(var b in targets)
            {
                var scroll=b.GetFirstAncestorOfType<ScrollView>();
                if(scroll!=null){scroll.ScrollTo(b);yield return null;yield return null;}
                var clip=scroll!=null?scroll.contentViewport.worldBound:Viewport;
                Check("layout.critical-action-reachable",Inside(b.worldBound,clip)&&Inside(b.worldBound,Viewport),Target(b),b.worldBound+" clip "+clip);
            }
            var pageScroll=_root.Q<ScrollView>(className:"product-scroll")??_root.Q<ScrollView>(className:"home-scroll");
            if(pageScroll!=null)pageScroll.scrollOffset=Vector2.zero;
        }
        private void AuditHud()
        {
            var blocks=new List<VisualElement>();
            foreach(string cls in new[]{"objective-card","mini-map","flight-bar","flight-bottom","hud-metric","touch-stick","flight-guidance"})
                blocks.AddRange(_root.Query<VisualElement>(className:cls).ToList().Where(Visible));
            foreach(var e in blocks)Check("layout.hud-contained",Inside(e.worldBound,Viewport),Target(e),e.worldBound.ToString());
            foreach(var metric in blocks.Where(b=>b.ClassListContains("hud-metric")))
                foreach(var label in metric.Query<Label>().ToList())
                    Check("layout.hud-content-contained",Inside(label.worldBound,metric.worldBound),Target(label),label.worldBound+" in "+metric.worldBound);
            foreach(var e in blocks.Where(b=>!b.ClassListContains("flight-bar")&&!b.ClassListContains("flight-bottom")))
            {
                var center=new Rect(Viewport.width*.30f,Viewport.height*.31f,Viewport.width*.40f,Viewport.height*.38f);
                Check("design.hud-central-view-clear",!GenuineOverlap(e.worldBound,center),Target(e),e.worldBound+" center "+center);
            }
            for(int i=0;i<blocks.Count;i++)for(int j=i+1;j<blocks.Count;j++)
                Check("layout.hud-blocks-no-overlap",!GenuineOverlap(blocks[i].worldBound,blocks[j].worldBound),Target(blocks[i])+" / "+Target(blocks[j]),blocks[i].worldBound+" / "+blocks[j].worldBound);
            var bar=_root.Q<VisualElement>(className:"flight-bar");var bottom=_root.Q<VisualElement>(className:"flight-bottom");
            Check("design.hud-top-max-48",bar.worldBound.height<=49,"HUD top",bar.worldBound.height.ToString("F1"));
            Check("design.hud-bottom-max-56",bottom.worldBound.height<=57,"HUD bottom",bottom.worldBound.height.ToString("F1"));
        }
        private IEnumerator Click(string title)
        {
            if(_mouse==null)_mouse=InputSystem.AddDevice<Mouse>("Layout QA Pointer");
            var b=_root.Query<Button>().ToList().FirstOrDefault(x=>x.text==title&&x.enabledInHierarchy);
            if(b==null){Check("interaction.button-exists",false,title);yield break;}
            var scroll=b.GetFirstAncestorOfType<ScrollView>();if(scroll!=null){scroll.ScrollTo(b);yield return Settle();}
            var p=b.worldBound.center;var point=new Vector2(p.x*Screen.width/_root.resolvedStyle.width,Screen.height-p.y*Screen.height/_root.resolvedStyle.height);
            InputSystem.QueueStateEvent(_mouse,new MouseState{position=point});yield return null;
            InputSystem.QueueStateEvent(_mouse,new MouseState{position=point,buttons=1});yield return null;yield return null;
            InputSystem.QueueStateEvent(_mouse,new MouseState{position=point});yield return Settle();
        }
        private IEnumerator Capture(string path)
        {
            yield return new WaitForEndOfFrame();var texture=ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path,texture.EncodeToPNG());Destroy(texture);
        }
    }
}
