// Independent supplemental batch. Does not modify the frozen 34-state matrix or product.
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
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
            if(_started||!Environment.GetCommandLineArgs().Contains("--space-submit-qa"))return;
            _started=true;var host=new GameObject("Independent Space Submit QA");UnityEngine.Object.DontDestroyOnLoad(host);host.AddComponent<SpaceSubmitProbe>();
        }
    }
    [Serializable] public class SpaceCheck { public string rule,evidence;public bool passed; }
    [Serializable] public class SpaceReport
    {
        public string contract="independent-space-submit-v1",version,startedUtc=DateTime.UtcNow.ToString("O"),status="NOT_RUN";
        public string limitations="Synthetic InputSystem keyboard/mouse, isolated desktop QA copy. No physical gamepad/iOS proof; no visual acceptance. Not the frozen 34-state matrix.";
        public int width,height,runtimeErrors;public List<SpaceCheck> checks=new();
    }
    public sealed class SpaceSubmitProbe:MonoBehaviour
    {
        private AeroSimApplication _app;private VisualElement _root;private Keyboard _keyboard;private Mouse _mouse;private Gamepad _gamepad;
        private SpaceReport _report=new();private int _submits,_navigation;private string _directory;
        private static string Arg(string key){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0&&i<a.Length-1?a[i+1]:null;}
        private void Track(string m,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)_report.runtimeErrors++;}
        private void OnEnable()=>Application.logMessageReceived+=Track;
        private void OnDisable()=>Application.logMessageReceived-=Track;
        private void Check(string rule,bool passed,string evidence="")=>_report.checks.Add(new SpaceCheck{rule=rule,passed=passed,evidence=evidence});
        private IEnumerator Settle(){yield return new WaitForSecondsRealtime(.10f);yield return null;yield return null;}
        private static bool Visible(VisualElement e)
        {
            if(e==null)return false;for(var n=e;n!=null;n=n.parent)if(n.resolvedStyle.display==DisplayStyle.None||n.resolvedStyle.visibility==Visibility.Hidden)return false;
            return e.worldBound.width>0&&e.worldBound.height>0;
        }
        private string FocusEvidence()
        {
            var f=_root.panel?.focusController.focusedElement as VisualElement;
            return "focus="+(f==null?"none":f.GetType().Name+"#"+f.name+" visible="+Visible(f))+" submitCount="+_submits;
        }
        private static string Title(Button b)=>!string.IsNullOrWhiteSpace(b.text)?b.text:b.Q<Label>(className:"design-button-label")?.text??b.tooltip??b.name;
        private Button FindButton(string title)=>_root.Query<Button>().ToList().FirstOrDefault(b=>Visible(b)&&b.enabledInHierarchy&&Title(b)==title);
        private IEnumerator KeyPress(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard,new KeyboardState(key));yield return null;yield return null;
            InputSystem.QueueStateEvent(_keyboard,new KeyboardState());yield return Settle();
        }
        private IEnumerator StickPress(Vector2 stick)
        {
            InputSystem.QueueStateEvent(_gamepad,new GamepadState{leftStick=stick});yield return null;yield return null;
            InputSystem.QueueStateEvent(_gamepad,new GamepadState());yield return Settle();
        }
        private IEnumerator GamepadSubmit()
        {
            InputSystem.QueueStateEvent(_gamepad,new GamepadState().WithButton(GamepadButton.South));yield return null;yield return null;
            InputSystem.QueueStateEvent(_gamepad,new GamepadState());yield return Settle();
        }
        private IEnumerator PointerClick(string title)
        {
            var b=FindButton(title);Check("pointer.target."+title,b!=null,FocusEvidence());if(b==null)yield break;
            var scroll=b.GetFirstAncestorOfType<ScrollView>();if(scroll!=null){scroll.ScrollTo(b);yield return Settle();}
            var point=b.worldBound.center;float scale=_app.GetComponent<UIDocument>().panelSettings.scale;
            var position=new Vector2(point.x*scale,Screen.height-point.y*scale);
            InputSystem.QueueStateEvent(_mouse,new MouseState{position=position});yield return null;
            InputSystem.QueueStateEvent(_mouse,new MouseState{position=position}.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));yield return null;yield return null;
            InputSystem.QueueStateEvent(_mouse,new MouseState{position=position});yield return Settle();
        }
        private IEnumerator FreshFlight(float text=1)
        {
            LocalStore.Profile.settings.textScale=text;LocalStore.Save();
            _app.ChangeWorkspace("Aerial");_app.StartFlight(new FlightConfig{workspace="Aerial",title="QA Space focus regression — not a pilot achievement"});
            _app.Simulator.Body.position=new Vector3(0,15,20);yield return Settle();
        }
        private IEnumerator SpaceMustNotAct(string stage)
        {
            var record=_app.Simulator.Record;bool paused=_app.Simulator.Paused,rth=_app.Simulator.ReturningHome;int photos=record.photos;
            string before=FocusEvidence();yield return KeyPress(Key.Space);
            Check("flight.no-unintended-submit."+stage,_app.CurrentPage==Page.Flight&&_app.Simulator.Running&&_app.Simulator.Record==record&&_app.Simulator.Paused==paused&&_app.Simulator.ReturningHome==rth&&record.photos==photos,before+" -> "+FocusEvidence()+" page="+_app.CurrentPage+" paused="+_app.Simulator.Paused+" rth="+_app.Simulator.ReturningHome);
            var f=_root.panel?.focusController.focusedElement as VisualElement;
            Check("focus.no-hidden-target."+stage,f==null||Visible(f),FocusEvidence());
        }
        private IEnumerator DirectionMustNotAct(string stage,bool gamepad)
        {
            var record=_app.Simulator.Record;var focused=_root.panel.focusController.focusedElement;
            bool paused=_app.Simulator.Paused,rth=_app.Simulator.ReturningHome;string before=FocusEvidence();
            if(gamepad)yield return StickPress(Vector2.down);else yield return KeyPress(Key.DownArrow);
            Check("flight.navigation-does-not-steal-focus."+stage,_root.panel.focusController.focusedElement==focused,before+" -> "+FocusEvidence());
            Check("flight.direction-no-unintended-action."+stage,_app.CurrentPage==Page.Flight&&_app.Simulator.Running&&_app.Simulator.Record==record&&_app.Simulator.Paused==paused&&_app.Simulator.ReturningHome==rth,before+" -> "+FocusEvidence());
        }
        private IEnumerator Start()
        {
            _directory=Arg("--space-submit-qa");
            if(Arg("--data-dir")==null||string.IsNullOrEmpty(_directory)){Debug.LogError("Isolated --data-dir and --space-submit-qa output are mandatory.");Application.Quit(2);yield break;}
            Directory.CreateDirectory(_directory);yield return Settle();
            _app=UnityEngine.Object.FindFirstObjectByType<AeroSimApplication>();_root=_app.GetComponent<UIDocument>().rootVisualElement;
            _report.version=Application.version;_report.width=Screen.width;_report.height=Screen.height;
            _root.RegisterCallback<NavigationSubmitEvent>(e=>_submits++,TrickleDown.TrickleDown);
            _root.RegisterCallback<NavigationMoveEvent>(e=>_navigation++,TrickleDown.TrickleDown);
            _keyboard=InputSystem.AddDevice<Keyboard>("Independent QA keyboard");_mouse=InputSystem.AddDevice<Mouse>("Independent QA mouse");
            _gamepad=InputSystem.AddDevice<Gamepad>("Independent synthetic QA gamepad — not physical hardware");
            LocalStore.Profile.onboarded=true;LocalStore.Profile.settings.uiScale="100%";LocalStore.Profile.settings.hudScale="100%";LocalStore.Profile.settings.textScale=1;LocalStore.Save();
            var actions=InputSystem.actions;var submit=actions?.FindAction("UI/Submit");var navigate=actions?.FindAction("UI/Navigate");
            Check("input.project-wide-actions-loaded",actions!=null,actions?.name);
            Check("input.space-submit-binding",submit!=null&&submit.bindings.Any(b=>b.path=="<Keyboard>/space"),submit?.ToString());
            Check("input.default-leftstick-preserved",navigate!=null&&navigate.bindings.Any(b=>b.path=="<Gamepad>/leftStick"));
            Check("input.default-joystick-preserved",navigate!=null&&navigate.bindings.Any(b=>b.path=="<Joystick>/stick"));
            _app.ChangeWorkspace("Aerial");_app.ShowPage(Page.Home);yield return Settle();
            var menuFirst=_root.Query<Button>().ToList().FirstOrDefault(b=>Visible(b)&&b.enabledInHierarchy);
            Check("menu.focus-target",menuFirst!=null);
            if(menuFirst!=null){menuFirst.Focus();yield return Settle();var oldFocus=_root.panel.focusController.focusedElement;int oldNavigation=_navigation;
                yield return StickPress(Vector2.down);Check("menu.gamepad-navigation",_navigation>oldNavigation&&_root.panel.focusController.focusedElement!=oldFocus,FocusEvidence());}
            _app.ChangeWorkspace("Aerial");_app.PreparePreview(Page.Preflight);yield return Settle();
            var toggles=_root.Query<Toggle>().ToList().Where(t=>Visible(t)&&t.enabledInHierarchy).ToList();
            Check("preflight.four-toggle-targets",toggles.Count==4,"count="+toggles.Count);
            foreach(var toggle in toggles)
            {
                var scroll=toggle.GetFirstAncestorOfType<ScrollView>();if(scroll!=null){scroll.ScrollTo(toggle);yield return Settle();}
                toggle.Focus();yield return Settle();bool original=toggle.value;int changes=0,submitted=_submits;
                toggle.RegisterValueChangedCallback(e=>changes++);
                yield return KeyPress(Key.Space);
                Check("toggle.space-flips-once",toggle.value!=original&&changes==1&&_submits==submitted+1,"value="+toggle.value+" changes="+changes+" submits="+(_submits-submitted)+" "+FocusEvidence());
                yield return KeyPress(Key.Space);
                Check("toggle.second-space-flips-once",toggle.value==original&&changes==2,"value="+toggle.value+" changes="+changes);
                if(!toggle.value)yield return KeyPress(Key.Space);
            }
            var launch=_root.Query<Button>().ToList().FirstOrDefault(b=>Visible(b)&&b.enabledInHierarchy&&b.ClassListContains("primary-button"));
            Check("preflight.launch-enabled-via-business-callback",launch!=null);
            if(launch!=null){launch.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(launch);yield return Settle();launch.Focus();yield return KeyPress(Key.Enter);}
            Check("preflight.enter-launch",_app.CurrentPage==Page.Flight);
            if(_app.CurrentPage==Page.Flight)yield return SpaceMustNotAct("after-real-preflight-launch");
            yield return FreshFlight();yield return SpaceMustNotAct("fresh-flight");
            yield return KeyPress(Key.Escape);
            var resume=FindButton("Resume Flight");Check("pause.resume-target",resume!=null);
            if(resume!=null){resume.Focus();yield return KeyPress(Key.Enter);}
            Check("pause.enter-resumes",!_app.Simulator.Paused);yield return SpaceMustNotAct("after-enter-resume");
            yield return DirectionMustNotAct("after-enter-resume-arrow",false);yield return DirectionMustNotAct("after-enter-resume-stick",true);
            yield return FreshFlight();yield return KeyPress(Key.Escape);yield return KeyPress(Key.Escape);
            Check("pause.escape-resumes",!_app.Simulator.Paused);yield return SpaceMustNotAct("after-escape-resume");
            yield return DirectionMustNotAct("after-escape-resume-arrow",false);yield return DirectionMustNotAct("after-escape-resume-stick",true);
            yield return FreshFlight();yield return KeyPress(Key.Escape);var pauseModal=_root.Query<VisualElement>(className:"modal-overlay").ToList().FirstOrDefault(Visible);
            var pauseFocus=_root.panel.focusController.focusedElement;int pauseNav=_navigation;yield return StickPress(Vector2.down);
            Check("pause.gamepad-navigation",_navigation>pauseNav&&_root.panel.focusController.focusedElement!=pauseFocus&&pauseModal!=null&&pauseModal.Contains(_root.panel.focusController.focusedElement as VisualElement),FocusEvidence());
            var gamepadResume=FindButton("Resume Flight");if(gamepadResume!=null){gamepadResume.Focus();yield return GamepadSubmit();}
            Check("pause.gamepad-submit-resumes",gamepadResume!=null&&!_app.Simulator.Paused,FocusEvidence());
            yield return SpaceMustNotAct("after-gamepad-resume");yield return DirectionMustNotAct("after-gamepad-resume-arrow",false);yield return DirectionMustNotAct("after-gamepad-resume-stick",true);
            yield return FreshFlight(1.25f);yield return PointerClick("Objective");
            Check("objective.opens-paused",_app.Simulator.Paused);yield return KeyPress(Key.Escape);
            Check("objective.escape-restores",!_app.Simulator.Paused);yield return SpaceMustNotAct("after-objective-close");
            yield return FreshFlight();yield return KeyPress(Key.Escape);yield return PointerClick("Exit Without Saving");yield return KeyPress(Key.Escape);
            Check("confirm.escape-stays-paused",_app.Simulator.Paused);
            var modal=_root.Query<VisualElement>(className:"modal-overlay").ToList().FirstOrDefault(Visible);
            Check("confirm.pause-focus-contained",modal!=null&&modal.Contains(_root.panel.focusController.focusedElement as VisualElement),FocusEvidence());
            yield return KeyPress(Key.Escape);yield return SpaceMustNotAct("after-confirm-cancel-and-resume");
            LocalStore.Profile.settings.textScale=1;LocalStore.Save();_app.ShowPage(Page.Home);yield return Settle();
            InputSystem.RemoveDevice(_keyboard);InputSystem.RemoveDevice(_mouse);InputSystem.RemoveDevice(_gamepad);
            _report.status=_report.runtimeErrors==0&&_report.checks.All(c=>c.passed)?"PASS_AUTOMATED_ONLY":"FAIL";
            File.WriteAllText(Path.Combine(_directory,"space-submit-report.json"),JsonUtility.ToJson(_report,true));
            Debug.Log("Independent Space Submit QA: "+_report.status+" checks="+_report.checks.Count);
            Application.Quit(_report.status=="PASS_AUTOMATED_ONLY"?0:2);
        }
    }
}
