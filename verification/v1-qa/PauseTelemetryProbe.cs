// Focused one-page native diagnostics. Does not replace frozen matrix or Space v1/v2/v3.
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
            if(_started||!Environment.GetCommandLineArgs().Contains("--pause-telemetry-qa"))return;
            _started=true;var host=new GameObject("Independent Pause Telemetry QA");UnityEngine.Object.DontDestroyOnLoad(host);host.AddComponent<PauseTelemetryProbe>();
        }
    }
    [Serializable] public class PauseCheck { public string rule,evidence;public bool passed; }
    [Serializable] public class PauseTrace { public string stage,evidence; }
    [Serializable] public class PauseReport
    {
        public string contract="independent-pause-telemetry-v1",status="NOT_RUN",version,startedUtc=DateTime.UtcNow.ToString("O");
        public string limitations="One Flight page with actual synthetic InputSystem pointer/key events, Text100/200. Desktop logical viewport only, not physical touch/iOS/safeArea or visual acceptance. Existing frozen reports/contracts unchanged.";
        public int width,height,runtimeErrors;public List<PauseCheck> checks=new();public List<PauseTrace> traces=new();
    }
    public sealed class PauseTelemetryProbe:MonoBehaviour
    {
        private AeroSimApplication _app;private VisualElement _root;private Keyboard _keyboard;private Mouse _mouse;private string _directory;private PauseReport _report=new();
        private static string Arg(string key){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);return i>=0&&i<args.Length-1?args[i+1]:null;}
        private void OnEnable()=>Application.logMessageReceived+=Log;
        private void OnDisable()=>Application.logMessageReceived-=Log;
        private void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)_report.runtimeErrors++;}
        private void Check(string rule,bool passed,string evidence="")=>_report.checks.Add(new PauseCheck{rule=rule,passed=passed,evidence=evidence});
        private void Trace(string stage,string evidence)=>_report.traces.Add(new PauseTrace{stage=stage,evidence=evidence});
        private IEnumerator Settle(){yield return new WaitForSecondsRealtime(.12f);yield return null;yield return null;}
        private static bool Visible(VisualElement e)
        {
            if(e==null||e.panel==null)return false;
            for(var p=e;p!=null;p=p.parent)if(p.resolvedStyle.display==DisplayStyle.None||p.resolvedStyle.visibility==Visibility.Hidden)return false;
            return e.worldBound.width>0&&e.worldBound.height>0;
        }
        private static string Title(Button b)=>!string.IsNullOrWhiteSpace(b.text)?b.text:b.Q<Label>(className:"design-button-label")?.text??b.tooltip??b.name;
        private Button Button(string title)=>_root.Query<Button>().ToList().LastOrDefault(b=>Visible(b)&&b.enabledInHierarchy&&Title(b)==title);
        private bool DialogClose()=>typeof(AeroSimApplication).GetField("_flightDialogClose",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(_app)!=null;
        private VisualElement PauseOverlay()=>typeof(AeroSimApplication).GetField("_pauseOverlay",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(_app) as VisualElement;
        private int ModalCount()=>_root.Query<VisualElement>(className:"modal-overlay").ToList().Count(Visible);
        private string State()
        {
            var focus=_root.panel.focusController.focusedElement as VisualElement;
            return "page="+_app.CurrentPage+" paused="+_app.Simulator.Paused+" running="+_app.Simulator.Running+" dialogClose="+DialogClose()+" modalCount="+ModalCount()+" focus="+(focus==null?"none":focus.GetType().Name+"#"+focus.name)+" record="+_app.Simulator.Record?.id;
        }
        private IEnumerator Key(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard,new KeyboardState(key));yield return null;yield return null;
            InputSystem.QueueStateEvent(_keyboard,new KeyboardState());yield return Settle();
        }
        private IEnumerator Click(string title,string stage)
        {
            var button=Button(title);Check("pointer.target",button!=null,stage+" "+title+" "+State());if(button==null)yield break;
            var scroll=button.GetFirstAncestorOfType<ScrollView>();scroll?.ScrollTo(button);yield return Settle();yield return Settle();
            var point=button.worldBound.center;var viewport=scroll?.contentViewport.worldBound??_root.worldBound;var picked=_root.panel.Pick(point);
            bool hit=false;string path="";
            for(var ancestor=picked;ancestor!=null;ancestor=ancestor.parent){path+=ancestor.GetType().Name+"#"+ancestor.name+" > ";if(ancestor==button)hit=true;}
            bool inside=button.worldBound.xMin>=viewport.xMin-3&&button.worldBound.xMax<=viewport.xMax+3&&button.worldBound.yMin>=viewport.yMin-3&&button.worldBound.yMax<=viewport.yMax+3;
            Trace(stage+" before-click","button="+title+" bounds="+button.worldBound+" viewport="+viewport+" pickedPath="+path+" hitButtonAncestor="+hit+" "+State());
            Check("pointer.scrolled-button-contained",inside,stage+" button="+button.worldBound+" viewport="+viewport);
            Check("pointer.pick-is-target-button",hit,stage+" "+path);
            float scale=_app.GetComponent<UIDocument>().panelSettings.scale;var position=new Vector2(point.x*scale,Screen.height-point.y*scale);
            InputSystem.QueueStateEvent(_mouse,new MouseState{position=position});yield return null;
            InputSystem.QueueStateEvent(_mouse,new MouseState{position=position}.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));yield return null;yield return null;
            InputSystem.QueueStateEvent(_mouse,new MouseState{position=position});yield return Settle();Trace(stage+" after-click",State());
        }
        private IEnumerator Capture(string name)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(_directory,name+".png"));yield return new WaitForEndOfFrame();yield return Settle();
        }
        private IEnumerator Fresh(float text,bool custom)
        {
            var settings=LocalStore.Profile.settings;LocalStore.Profile.onboarded=true;settings.uiScale="100%";settings.hudScale="100%";settings.textScale=text;
            settings.osdSpeed=settings.osdAltitude=settings.osdBattery=settings.osdTimer=true;
            settings.speedPosition=custom?new Vector2(.5f,.4f):new Vector2(.6f,.78f);settings.altitudePosition=custom?new Vector2(.5f,.4f):new Vector2(.23f,.78f);
            settings.batteryPosition=custom?new Vector2(.5f,.4f):new Vector2(.78f,.32f);settings.timerPosition=custom?new Vector2(.5f,.4f):new Vector2(.46f,.13f);LocalStore.Save();
            _app.ChangeWorkspace("Aerial");_app.StartFlight(new FlightConfig{workspace="Aerial",title="QA Pause/Telemetry diagnostic — NOT pilot achievement"});_app.Simulator.Body.position=new Vector3(10,15,20);yield return Settle();yield return Settle();
            foreach(var metric in _root.Query<VisualElement>().ToList().Where(e=>e.name=="osd-speed"||e.name=="osd-altitude"||e.name=="osd-battery"||e.name=="osd-timer"))
                Trace("OSD Text="+text+" custom="+custom,metric.name+" visible="+Visible(metric)+" reason="+metric.userData+" bounds="+metric.worldBound+" tooltip="+metric.tooltip);
        }
        private string ProfileDisk()=>File.ReadAllText(Path.Combine(LocalStore.DirectoryPath,"profile.json"));
        private string Length(float meters)=>LocalStore.Profile.settings.units=="Metric"?meters.ToString("F0")+" m":(meters*3.28084f).ToString("F0")+" ft";
        private string Speed(float mps)=>LocalStore.Profile.settings.units=="Metric"?(mps*3.6f).ToString("F1")+" km/h":(mps*2.23694f).ToString("F1")+" mph";
        private IEnumerator Start()
        {
            _directory=Arg("--pause-telemetry-qa");if(string.IsNullOrEmpty(_directory)||Arg("--data-dir")==null){Debug.LogError("Isolated --data-dir and --pause-telemetry-qa output required.");Application.Quit(2);yield break;}
            Directory.CreateDirectory(_directory);yield return Settle();_app=UnityEngine.Object.FindFirstObjectByType<AeroSimApplication>();_root=_app.GetComponent<UIDocument>().rootVisualElement;
            _report.version=Application.version;_report.width=Screen.width;_report.height=Screen.height;
            _keyboard=InputSystem.AddDevice<Keyboard>("Independent Pause QA keyboard");_mouse=InputSystem.AddDevice<Mouse>("Independent Pause QA mouse");
            foreach(float text in new[]{1f,2f})
            {
                string stage="Text"+Mathf.RoundToInt(text*100);
                yield return Fresh(text,false);yield return Click("Pause",stage+" pause");
                Check("pause.opens",_app.Simulator.Paused&&!DialogClose()&&ModalCount()==1,stage+" "+State());
                var record=_app.Simulator.Record;string frozenRecord=JsonUtility.ToJson(record),profile=JsonUtility.ToJson(LocalStore.Profile),disk=ProfileDisk();
                yield return Click("Exit Without Saving",stage+" exit");
                Check("confirm.opens-nested",_app.Simulator.Paused&&DialogClose()&&ModalCount()==2,stage+" "+State());yield return Capture(stage+"-nested-confirm");
                yield return Key(UnityEngine.InputSystem.Key.Escape);Trace(stage+" after-confirm-Escape",State());
                Check("confirm.escape-restores-pause",_app.CurrentPage==Page.Flight&&_app.Simulator.Paused&&!DialogClose()&&ModalCount()==1&&Visible(PauseOverlay()),stage+" "+State());
                Check("confirm.record-unchanged",ReferenceEquals(record,_app.Simulator.Record)&&JsonUtility.ToJson(record)==frozenRecord,stage);
                Check("confirm.profile-memory-disk-unchanged",JsonUtility.ToJson(LocalStore.Profile)==profile&&ProfileDisk()==disk,stage);yield return Capture(stage+"-after-confirm-Escape");

                // A fresh custom-OSD flight isolates Telemetry from a failed earlier confirmation.
                yield return Fresh(text,true);yield return Capture(stage+"-custom-osd-running");yield return Click("Pause",stage+" telemetry-pause");
                Check("telemetry.pause-opens",_app.Simulator.Paused&&ModalCount()==1,stage+" "+State());
                record=_app.Simulator.Record;frozenRecord=JsonUtility.ToJson(record);profile=JsonUtility.ToJson(LocalStore.Profile);disk=ProfileDisk();
                var sim=_app.Simulator;string[] expected={"Altitude   "+Length(sim.Position.y),"Distance from home   "+Length(new Vector2(sim.Position.x,sim.Position.z).magnitude),"Horizontal speed   "+Speed(new Vector2(sim.Body.linearVelocity.x,sim.Body.linearVelocity.z).magnitude),"Vertical speed   "+Speed(sim.Body.isKinematic?0:sim.Body.linearVelocity.y),"Battery   "+sim.Battery.ToString("F0")+"%","Elapsed   "+TimeSpan.FromSeconds(Mathf.Max(0,sim.Elapsed)).ToString(@"mm\:ss")};
                yield return Click("Flight Telemetry",stage+" telemetry-open");Check("telemetry.opens-nested-paused",sim.Paused&&DialogClose()&&ModalCount()==2,stage+" "+State());
                var overlay=_root.Query<VisualElement>(className:"modal-overlay").ToList().LastOrDefault(Visible);
                foreach(var value in expected)
                {
                    var label=overlay?.Query<Label>().ToList().FirstOrDefault(e=>e.text==value);Check("telemetry.actual-value",label!=null,stage+" expected="+value);
                    if(label!=null){label.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(label);yield return Settle();var viewport=label.GetFirstAncestorOfType<ScrollView>()?.contentViewport.worldBound??_root.worldBound;var required=label.MeasureTextSize(label.text,label.contentRect.width,VisualElement.MeasureMode.Exactly,0,VisualElement.MeasureMode.Undefined);
                        Check("telemetry.value-readable-after-scroll",Visible(label)&&label.contentRect.height+3>=required.y&&label.worldBound.yMin>=viewport.yMin-3&&label.worldBound.yMax<=viewport.yMax+3,stage+" "+value+" bounds="+label.worldBound+" viewport="+viewport+" required="+required);}
                }
                yield return Capture(stage+"-telemetry-sixth-value");yield return Click("Back to Flight",stage+" telemetry-back");
                Check("telemetry.back-restores-original-pause",_app.CurrentPage==Page.Flight&&sim.Paused&&!DialogClose()&&ModalCount()==1&&Visible(PauseOverlay()),stage+" "+State());
                Check("telemetry.original-record-unchanged",ReferenceEquals(record,sim.Record)&&JsonUtility.ToJson(record)==frozenRecord,stage);
                Check("telemetry.profile-memory-disk-unchanged",JsonUtility.ToJson(LocalStore.Profile)==profile&&ProfileDisk()==disk,stage);yield return Capture(stage+"-telemetry-back-paused");
            }
            InputSystem.RemoveDevice(_keyboard);InputSystem.RemoveDevice(_mouse);
            _report.status=_report.runtimeErrors==0&&_report.checks.All(c=>c.passed)?"PASS_AUTOMATED_ONLY":"FAIL";
            File.WriteAllText(Path.Combine(_directory,"pause-telemetry-report.json"),JsonUtility.ToJson(_report,true));Debug.Log("Independent Pause Telemetry QA "+_report.status+" checks="+_report.checks.Count);Application.Quit(_report.status=="PASS_AUTOMATED_ONLY"?0:2);
        }
    }
}
