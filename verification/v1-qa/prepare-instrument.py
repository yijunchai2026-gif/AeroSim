#!/usr/bin/env python3
"""Adapt historical QA in a temp directory; never edit the historical source/contract."""
import argparse
import hashlib
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
KEYBOARD_PROBE = r'''
        private SessionRecord BuildSyntheticDesignFixture()
        {
            var r=new SessionRecord{id="ui-qa-fixture",startedUtc="2026-10-08T00:00:00Z",title="QA synthetic RTH — NOT a pilot achievement",workspace="Aerial",environment="Pacific Coast",aircraft="DJI Mavic 3 Pro",duration=200,score=88,passed=true,distance=610,maxAltitude=24.5f,feedback="Synthetic QA samples/events only; not actual flight telemetry or a pilot achievement.",courseId="",mode="Normal"};
            r.config=CourseCatalog.Find("a10").Config();r.config.aircraft=r.aircraft;r.config.title=r.title;
            for(int i=0;i<=40;i++)
            {
                float t=i/40f;float angle=t*Mathf.PI*2;
                r.frames.Add(new TelemetryFrame{time=i*5,position=new Vector3(Mathf.Sin(angle)*28,8+Mathf.Sin(t*Mathf.PI)*16,10+(1-Mathf.Cos(angle))*28),rotation=new Vector3(0,t*360,0),battery=100-t*16,speed=Mathf.Sin(t*Mathf.PI)*6,input=new Vector4(Mathf.Sin(angle)*.2f,0,Mathf.Cos(angle)*.2f,0)});
            }
            r.events.Add(new FlightEvent{time=0,kind="qa-synthetic",message="QA synthetic: takeoff"});
            r.events.Add(new FlightEvent{time=35,kind="qa-synthetic",message="QA synthetic: steady hover"});
            r.events.Add(new FlightEvent{time=95,kind="qa-synthetic",message="QA synthetic: home-point check"});
            r.events.Add(new FlightEvent{time=145,kind="qa-synthetic",message="QA synthetic: Return-to-Home"});
            r.events.Add(new FlightEvent{time=200,kind="qa-synthetic",message="QA synthetic: landing / passed layout state"});
            // No fake image paths, captures, gate counts, or photographic-quality claims.
            return r;
        }
        private IEnumerator InspectFailureFixture(float scale,float text)
        {
            var failed=JsonUtility.FromJson<SessionRecord>(JsonUtility.ToJson(_fixture));
            failed.id="ui-qa-failure-fixture";failed.title="QA synthetic failure — NOT a pilot achievement";failed.passed=false;failed.score=32;
            failed.frames.Clear();failed.events.Clear();failed.duration=20;failed.distance=0;failed.maxAltitude=0;
            failed.frames.Add(new TelemetryFrame{time=0,position=new Vector3(0,8,10),battery=100});
            failed.frames.Add(new TelemetryFrame{time=20,position=new Vector3(10,12,25),battery=99});
            LocalStore.Profile.sessions.Remove(failed.id);LocalStore.SaveSession(failed);
            _app.PreparePreview(Page.Results);yield return Settle();yield return Inspect("Aerial-ResultsFailure",scale,text);
            LocalStore.Profile.sessions.Remove(failed.id);LocalStore.Save();
        }
        private Keyboard _keyboard;
        private bool _qaInputObserved;
        private int _qaKeyDownCount,_qaNavigationCount,_qaSubmitCount;
        private string _qaLastKey="none",_qaLastNavigation="none";
        private void ObserveQaInput()
        {
            if(_qaInputObserved)return;_qaInputObserved=true;
            // Observation only: never consume events, redirect focus, or toggle values.
            _root.RegisterCallback<KeyDownEvent>(e=>{_qaKeyDownCount++;_qaLastKey=e.keyCode.ToString();},TrickleDown.TrickleDown);
            _root.RegisterCallback<NavigationMoveEvent>(e=>{_qaNavigationCount++;_qaLastNavigation=e.direction.ToString();},TrickleDown.TrickleDown);
            _root.RegisterCallback<NavigationSubmitEvent>(e=>{_qaSubmitCount++;},TrickleDown.TrickleDown);
        }
        private string QaInputEvidence()
        {
            var focused=_root.panel?.focusController.focusedElement as VisualElement;
            string path="";for(var e=focused;e!=null;e=e.parent)path=e.GetType().Name+"#"+e.name+"/"+path;
            return "focus="+path+" keyDown="+_qaKeyDownCount+" lastKey="+_qaLastKey+" navigation="+_qaNavigationCount+" lastNavigation="+_qaLastNavigation+" submit="+_qaSubmitCount;
        }
        private IEnumerator QaPress(Key key)
        {
            if(_keyboard==null)_keyboard=InputSystem.AddDevice<Keyboard>("V1 QA Keyboard");
            InputSystem.QueueStateEvent(_keyboard,new KeyboardState(key));
            yield return null;yield return null;
            InputSystem.QueueStateEvent(_keyboard,new KeyboardState());yield return Settle();
        }
        private static string QaButtonTitle(Button button)
        {
            if(!string.IsNullOrWhiteSpace(button.text))return button.text;
            return button.Q<Label>(className:"design-button-label")?.text??button.tooltip??button.name;
        }
        private IEnumerator FocusPrimary(string stage,string semanticAction=null)
        {
            var b=_root.Query<Button>().ToList().FirstOrDefault(x=>Visible(x)&&x.enabledInHierarchy&&(semanticAction!=null?QaButtonTitle(x)==semanticAction:x.ClassListContains("primary-button")));
            Check("interaction.keyboard-primary-exists",b!=null,stage);if(b==null)yield break;
            var scroll=b.GetFirstAncestorOfType<ScrollView>();if(scroll!=null){scroll.ScrollTo(b);yield return Settle();}
            b.Focus();yield return Settle();
            Check("interaction.keyboard-focus",_root.panel.focusController.focusedElement==b,stage);
            yield return QaPress(Key.Enter);
        }
        private IEnumerator KeyboardJourney()
        {
            BeginCase("Keyboard-MainFlow",1,1);
            ObserveQaInput();
            _app.ChangeWorkspace("Aerial");_app.PreparePreview(Page.Home);yield return Settle();
            var first=_root.Query<Button>().ToList().FirstOrDefault(x=>Visible(x)&&x.enabledInHierarchy);
            if(first!=null){first.Focus();yield return Settle();yield return QaPress(Key.Tab);}
            Check("interaction.keyboard-tab-navigation",first!=null&&_root.panel.focusController.focusedElement!=null&&_root.panel.focusController.focusedElement!=first,"Home Tab");
            yield return FocusPrimary("Home primary");Check("interaction.keyboard-home-route",_app.CurrentPage!=Page.Home,"Home Enter");
            _app.PreparePreview(Page.Academy);yield return Settle();yield return FocusPrimary("Academy lesson");
            Check("interaction.keyboard-course-route",_app.CurrentPage==Page.Brief,"Academy Enter -> Brief");
            _app.PreparePreview(Page.Brief);yield return Settle();yield return FocusPrimary("Brief next");
            Check("interaction.keyboard-preflight-route",_app.CurrentPage==Page.Preflight,"Brief Enter -> Preflight");
            foreach(var toggle in _root.Query<Toggle>().ToList().Where(Visible))
            {
                if(!toggle.enabledInHierarchy||toggle.value)continue;
                var scroll=toggle.GetFirstAncestorOfType<ScrollView>();if(scroll!=null){scroll.ScrollTo(toggle);yield return Settle();}
                toggle.Focus();yield return Settle();string before=QaInputEvidence();
                yield return QaPress(Key.Space);Check("interaction.keyboard-preflight-toggle",toggle.value,Target(toggle),"focusable="+toggle.focusable+" before="+before+" after="+QaInputEvidence());
            }
            yield return FocusPrimary("Preflight launch");Check("interaction.keyboard-launch",_app.CurrentPage==Page.Flight,"Preflight Enter -> Flight");
            if(_app.CurrentPage!=Page.Flight)_app.StartFlight(new FlightConfig());
            yield return Settle();yield return QaPress(Key.Escape);
            var modal=_root.Q<VisualElement>(className:"modal-overlay");
            Check("interaction.keyboard-pause",_app.Simulator.Paused&&modal!=null,"Escape opens pause");
            if(modal!=null)
            {
                var button=modal.Query<Button>().ToList().FirstOrDefault(x=>Visible(x)&&x.enabledInHierarchy);if(button!=null)button.Focus();
                for(int i=0;i<12;i++)
                {
                    yield return QaPress(Key.Tab);var focused=_root.panel.focusController.focusedElement as VisualElement;
                    Check("interaction.keyboard-modal-trap",focused!=null&&modal.Contains(focused),"Pause Tab "+i,QaInputEvidence());
                }
                var resume=modal.Query<Button>().ToList().FirstOrDefault(x=>x.text=="Resume Flight");
                if(resume!=null){resume.Focus();yield return QaPress(Key.Enter);}
                Check("interaction.keyboard-resume",!_app.Simulator.Paused,"Resume Enter");
            }
            _app.PreparePreview(Page.Results);yield return Settle();yield return FocusPrimary("Results replay","Replay Flight");
            Check("interaction.keyboard-replay",_app.CurrentPage==Page.Replay,"Results Enter -> Replay");
            _app.ShowPage(Page.Home);yield return Settle();
            if(_keyboard!=null){InputSystem.RemoveDevice(_keyboard);_keyboard=null;}
        }
        private IEnumerator ModalAndOsdRegression()
        {
            BeginCase("Interaction-Modal-OSD-ReplayContext",1,1);
            var originalSpeed=LocalStore.Profile.settings.speedPosition;
            LocalStore.Profile.settings.textScale=1.25f;
            _app.StartFlight(new FlightConfig{workspace="Aerial"});yield return Settle();
            yield return Click("Objective");yield return Settle();
            Check("interaction.objective-pauses-reading",_app.Simulator.Paused&&_root.Query<VisualElement>(className:"modal-overlay").ToList().Any(Visible),"Objective opens paused modal");
            yield return QaPress(Key.Escape);
            Check("interaction.objective-escape-restores",!_app.Simulator.Paused&&!_root.Query<VisualElement>(className:"modal-overlay").ToList().Any(Visible),"Escape closes Objective and restores originally running flight");
            // Restart a clean test session rather than letting a known modal bug corrupt later assertions.
            _app.StartFlight(new FlightConfig{workspace="Aerial"});yield return Settle();
            yield return Click("Pause");yield return Click("Exit Without Saving");
            Check("interaction.exit-confirm-remains-paused",_app.Simulator.Paused,"Exit confirmation does not resume physics");
            yield return QaPress(Key.Escape);
            Check("interaction.nested-confirm-escape",_app.Simulator.Paused&&_root.Query<VisualElement>(className:"modal-overlay").ToList().Count(Visible)==1,"Escape cancels confirmation and returns to paused menu");
            LocalStore.Profile.settings.textScale=1;LocalStore.Profile.settings.osdSpeed=true;
            LocalStore.Profile.settings.speedPosition=new Vector2(.77f,.20f);LocalStore.Save();LocalStore.Initialize();
            Check("regression.osd-anchor-persists",Vector2.Distance(LocalStore.Profile.settings.speedPosition,new Vector2(.77f,.20f))<.001f,"OSD saved anchor reload");
            _app.StartFlight(new FlightConfig{workspace="FPV",mode="Normal"});yield return Settle();
            var speed1=_root.Query<Label>(className:"hud-caption").ToList().FirstOrDefault(x=>x.text=="H.S")?.parent;
            var rect1=speed1!=null?speed1.worldBound:new Rect();
            LocalStore.Profile.settings.speedPosition=new Vector2(.77f,.70f);
            _app.RefreshUiPreferences();yield return Settle();
            var speed2=_root.Query<Label>(className:"hud-caption").ToList().FirstOrDefault(x=>x.text=="H.S")?.parent;
            Check("regression.osd-anchor-affects-flight",speed1!=null&&speed2!=null&&Vector2.Distance(rect1.position,speed2.worldBound.position)>8,"Two safe FPV anchors change actual in-flight metric position",rect1+" -> "+speed2?.worldBound);
            var replay=JsonUtility.FromJson<SessionRecord>(JsonUtility.ToJson(_fixture));replay.workspace="FPV";replay.config.workspace="FPV";
            _app.ChangeWorkspace("Professional");_app.BeginReplay(replay);yield return Settle();
            var chip=_root.Q<Label>(className:"workspace-label");
            Check("regression.replay-workspace-context",chip!=null&&chip.text.Contains("FPV"),"FPV recorded session identifies its workspace from Professional entry",chip?.text);
            LocalStore.Profile.settings.speedPosition=originalSpeed;LocalStore.Profile.settings.textScale=1;LocalStore.Save();
            _app.ChangeWorkspace("Aerial");yield return Settle();
            if(_keyboard!=null){InputSystem.RemoveDevice(_keyboard);_keyboard=null;}
        }
'''

DESIGN_GATE = r'''
        private void AuditDesignShell()
        {
            if(!_root.ClassListContains("design-v1"))return;
            var bar=_root.Q<VisualElement>(className:"top-bar");
            if(bar!=null&&Visible(bar))
            {
                Check("design-v1.topbar-contained",Inside(bar.worldBound,Viewport),"Top bar",bar.worldBound.ToString());
                if(_case.textMultiplier==1&&Viewport.width>=1280)
                    Check("design-v1.topbar-height-88",Mathf.Abs(bar.worldBound.height-88)<1.1f,"Picture-led top bar",bar.worldBound.height.ToString("F1"));
            }
            var nav=_root.Q<ScrollView>(className:"side-nav");
            if(nav!=null&&Visible(nav))
            {
                float expected=Viewport.width>=1280?248:72;
                Check("design-v1.navigation-width",Mathf.Abs(nav.worldBound.width-expected)<2,"Picture-led navigation",nav.worldBound.width+" expected "+expected);
                Check("design-v1.navigation-contained",Inside(nav.worldBound,Viewport),"Navigation",nav.worldBound.ToString());
            }
            if(Viewport.width>=1280&&Viewport.height>=500&&_case.textMultiplier==1)
                foreach(var columns in _root.Query<VisualElement>(className:"design-columns").ToList().Where(Visible))
                    foreach(var child in columns.Children().Where(e=>Visible(e)&&e.ClassListContains("design-column")))
                        Check("layout.design-main-column-readable",child.worldBound.width>=160,"Main column "+Target(child),child.worldBound+" parent="+columns.worldBound+" flexGrow="+child.resolvedStyle.flexGrow+" flexBasis="+child.resolvedStyle.flexBasis);
        }
        private void AuditDesignHud()
        {
            if(_root.ClassListContains("v1-replay"))
            {
                var view=_root.Q<VisualElement>(className:"v1-replay-viewport");
                Check("design-v1.replay-view-present",view!=null,"Replay viewport");
                Check("regression.replay-real-rendertexture",_app.Simulator.View!=null&&_app.Simulator.View.targetTexture!=null&&_app.Simulator.View.targetTexture.IsCreated(),"Replay camera draws live recorded scene");
                foreach(var e in _root.Query<VisualElement>().ToList().Where(e=>Visible(e)&&(e.ClassListContains("v1-replay-viewport")||e.ClassListContains("v1-replay-telemetry")||e.ClassListContains("v1-replay-timeline"))))
                {
                    var scroll=e.GetFirstAncestorOfType<ScrollView>();var clip=scroll!=null?scroll.contentViewport.worldBound:Viewport;
                    Check("layout.replay-horizontal-containment",e.worldBound.xMin>=clip.xMin-2&&e.worldBound.xMax<=clip.xMax+2,Target(e),e.worldBound+" in "+clip);
                }
                return;
            }
            var bar=_root.Q<VisualElement>(className:"v1-flight-bar");
            var telemetry=_root.Q<VisualElement>(className:"v1-flight-telemetry");
            Check("design-v1.hud-bar-present",bar!=null,"HUD top");
            Check("design-v1.hud-telemetry-present",telemetry!=null,"HUD telemetry");
            if(bar!=null){Check("layout.hud-contained",Inside(bar.worldBound,Viewport),"HUD top",bar.worldBound.ToString());Check("design-v1.hud-top-height-88",Mathf.Abs(bar.worldBound.height-88)<1.1f,"Picture-led HUD top",bar.worldBound.height.ToString("F1"));}
            var blocks=new List<VisualElement>();
            foreach(string cls in new[]{"v1-objective-scroll","mini-map","hud-metric","touch-stick","flight-guidance","v1-camera-tools","v1-compact-actions","v1-race-timer"})
                blocks.AddRange(_root.Query<VisualElement>(className:cls).ToList().Where(Visible));
            if(telemetry!=null&&Visible(telemetry)){blocks.Add(telemetry);if(!_root.ClassListContains("hud-compact"))Check("design-v1.hud-telemetry-height-80",Mathf.Abs(telemetry.worldBound.height-80)<1.1f,"Picture-led telemetry",telemetry.worldBound.ToString("F1"));}
            foreach(var e in blocks)
            {
                Check("layout.hud-contained",Inside(e.worldBound,Viewport),Target(e),e.worldBound.ToString());
                var center=new Rect(Viewport.width*.30f,Viewport.height*.31f,Viewport.width*.40f,Viewport.height*.38f);
                Check("design.hud-central-view-clear",!GenuineOverlap(e.worldBound,center),Target(e),e.worldBound+" center "+center);
                if(e.ClassListContains("hud-metric"))foreach(var label in e.Query<Label>().ToList())
                    Check("layout.hud-content-contained",Inside(label.worldBound,e.worldBound),Target(label),label.worldBound+" in "+e.worldBound);
            }
            for(int i=0;i<blocks.Count;i++)for(int j=i+1;j<blocks.Count;j++)
                if(!blocks[i].Contains(blocks[j])&&!blocks[j].Contains(blocks[i]))
                    Check("layout.hud-blocks-no-overlap",!GenuineOverlap(blocks[i].worldBound,blocks[j].worldBound),Target(blocks[i])+" / "+Target(blocks[j]),blocks[i].worldBound+" / "+blocks[j].worldBound);
        }
'''


def transform(source):
    def replace(old, new):
        nonlocal source
        if source.count(old) != 1:
            raise ValueError("Historical QA anchor changed: " + old[:80])
        source = source.replace(old, new)
    replace('foreach(float scale in scales)yield return Matrix(scale,1);',
            'foreach(float scale in scales)foreach(var textValue in Arg("--qa-text-scales","1,1.25,1.5,2").Split(\',\'))yield return Matrix(scale,float.Parse(textValue,CultureInfo.InvariantCulture));')
    replace('if(Arg("--qa-text-stress")=="true")yield return Matrix(1,2);', '// Full UI/HUD x Text matrix above.')
    replace('yield return PreferencesRegression();', 'yield return PreferencesRegression();\n            yield return KeyboardJourney();\n            yield return ModalAndOsdRegression();')
    replace('private IEnumerator PreferencesRegression()', KEYBOARD_PROBE + '\n        private IEnumerator PreferencesRegression()')
    old_fixture = '''_fixture=new SessionRecord{id="ui-qa-fixture",startedUtc="2026-10-05T00:00:00Z",title="QA fixture — not a pilot achievement",workspace="Aerial",environment="Pacific Coast",aircraft="DJI Mavic 3 Pro",duration=20,score=84,feedback="Synthetic layout test fixture only."};
            _fixture.config=new FlightConfig{aircraft="DJI Mavic 3 Pro",title=_fixture.title};
            _fixture.frames.Add(new TelemetryFrame{time=0,position=new Vector3(0,8,10),battery=100});
            _fixture.frames.Add(new TelemetryFrame{time=20,position=new Vector3(10,12,25),battery=99});'''
    replace(old_fixture, '_fixture=BuildSyntheticDesignFixture();')
    replace('if(workspace=="Aerial")\n                {\n                    _app.ShowPage(Page.FreeFlight);', 'if(workspace=="Aerial")\n                {\n                    yield return InspectFailureFixture(scale,text);\n                    _app.ShowPage(Page.FreeFlight);')
    replace('_app.Simulator.Body.position=new Vector3(0,8,10);_app.Simulator.Pause(true);_app.Simulator.Body.transform.position=_app.Simulator.Body.position;', '''if(workspace!="Professional")
                {
                    _app.Simulator.Body.position=new Vector3(0,8,10);_app.Simulator.Pause(true);_app.Simulator.Body.transform.position=_app.Simulator.Body.position;
                    yield return Settle();yield return Inspect(workspace+"-FlightGeneric",scale,text);
                    if(workspace=="Aerial")_app.StartFlight(CourseCatalog.Find("a10").Config());
                    else _app.StartFlight(new FlightConfig{workspace="FPV",environment="Warehouse",aircraft="DJI Avata 2",mode="Manual",race=true,task="gates",courseId="race-Warehouse",title="Warehouse Circuit",objective="Fly through all 6 gates in order with as few collisions as possible."});
                }
                _app.Simulator.Body.position=new Vector3(0,8,10);_app.Simulator.Pause(true);_app.Simulator.Body.transform.position=_app.Simulator.Body.position;''')
    replace('private void AuditHud()', DESIGN_GATE + '\n        private void AuditLegacyHud()')
    replace('if(state.EndsWith("-Flight")||state=="Aerial-Replay")AuditHud();',
            'AuditDesignShell();\n            if(state.EndsWith("-Flight")||state.EndsWith("-FlightGeneric")||state=="Aerial-Replay"){if(_root.ClassListContains("v1-flight-hud")||_root.ClassListContains("v1-replay"))AuditDesignHud();else AuditLegacyHud();}')
    replace('public int width,height,runtimeErrors;public List<QaCase>', 'public string qaContractVersion="v1-candidate02-34states";public string fixtureDescription="Results/Replay/Report: explicit synthetic QA passed RTH record, 41 frames and 5 synthetic events; zero fabricated photo evidence. Additional failure and generic-flight states retained. FPVFlight: real Warehouse Manual race; AerialFlight: real a10 Return-to-Home config.";public int width,height,runtimeErrors;public List<QaCase>')
    replace('x=>x.text==title&&x.enabledInHierarchy', 'x=>Visible(x)&&QaButtonTitle(x)==title&&x.enabledInHierarchy')
    source, count = re.subn(r'bool screenshot=.*?;\s*if\(screenshot\)', 'bool screenshot=true;\n            if(screenshot)', source, count=1, flags=re.S)
    if count != 1:
        raise ValueError("Historical screenshot anchor changed")
    replace('"-text"+text.ToString("0")', '"-text"+(text*100).ToString("0",CultureInfo.InvariantCulture)')
    source = source.replace('"design.topbar-height-64"', '"legacy-prd.topbar-height-64"')
    source = source.replace('"design.nav-width"', '"legacy-prd.nav-width"')
    source = source.replace('"AeroSim 0.4 UI candidate COPY"', '"AeroSim 1.0 candidate COPY — independent native QA"')
    return source


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    output = args.output.resolve()
    if not str(output).startswith(("/private/tmp/aerosim-uiqa.", "/tmp/aerosim-uiqa.")):
        parser.error("Only an explicit temporary aerosim-uiqa.* directory is permitted")
    original = (ROOT / "verification/ui-qa-tools/LayoutQa.cs").read_text()
    output.write_text(transform(original))
    print("Historical native QA SHA256: " + hashlib.sha256(original.encode()).hexdigest())
    print("Prepared full matrix/real InputSystem keyboard source: " + str(output))


if __name__ == "__main__":
    main()
