// Independent diagnostics; compile with mechanical hash-locked v1/v2 temp sources.
using System;
using System.Linq;
using System.Collections;
using System.IO;
using AeroSim.Domain;
using AeroSim.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

namespace AeroSim.Qa
{
    [Serializable] public class PresentationNoteV3
    {
        public string classification="UNRENDERED",page,text,ancestorEvidence;
        public float textScale;public string bounds;
        public string reason="Closed Advanced Input Mapping Foldout contentContainer descendant; not a PASS and not a general zero-geometry exemption.";
    }
    public sealed partial class SpaceSubmitProbeV3
    {
        private Foldout V3ClosedMappingAncestor(TextElement element)
        {
            for(var ancestor=element.parent;ancestor!=null;ancestor=ancestor.parent)
                if(ancestor is Foldout foldout && foldout.text=="Advanced Input Mapping" && !foldout.value && foldout.contentContainer.Contains(element))return foldout;
            return null;
        }
        private bool V3Presented(TextElement element,string page,float text)
        {
            var foldout=V3ClosedMappingAncestor(element);
            if(foldout!=null&&!string.IsNullOrWhiteSpace(element.text))
            {
                var ancestry="";
                for(VisualElement ancestor=element;ancestor!=null;ancestor=ancestor.parent)
                {
                    ancestry+=ancestor.GetType().Name+"#"+ancestor.name+" display="+ancestor.resolvedStyle.display+" visibility="+ancestor.resolvedStyle.visibility+" inlineOverflow="+ancestor.style.overflow+" rect="+ancestor.worldBound+"; ";
                    if(ancestor==foldout)break;
                }
                _report.presentationNotes.Add(new PresentationNoteV3{page=page,textScale=text,text=element.text,bounds=element.worldBound.ToString(),ancestorEvidence="Foldout.value=False; contentContainer.Contains=True; "+ancestry});
                return false;
            }
            return V2Displayed(element);
        }

        private void V3AuditTarget(TextElement element,string stage,bool staticInput=false)
        {
            Check("v3.rendered-target-exists",element!=null,stage);if(element==null)return;
            var box=element.contentRect;float font=element.resolvedStyle.fontSize;
            var required=element.MeasureTextSize(element.text,box.width,element.resolvedStyle.whiteSpace==WhiteSpace.Normal?VisualElement.MeasureMode.Exactly:VisualElement.MeasureMode.Undefined,0,VisualElement.MeasureMode.Undefined);
            string evidence=stage+" text="+element.text+" content="+box+" font="+font+" required="+required+" world="+element.worldBound;
            Check("v3.rendered-target-positive",V2Displayed(element)&&box.width>0&&box.height>0&&font>0,evidence);
            Check("v3.rendered-linebox-height",box.height+3>=required.y,evidence);
            if(element.resolvedStyle.whiteSpace!=WhiteSpace.Normal)
                Check(staticInput?"v3.static-input-value-width":"v3.rendered-singleline-width",box.width+3>=required.x,evidence);
            var scroll=element.GetFirstAncestorOfType<ScrollView>();
            var viewport=scroll?.contentViewport.worldBound??_root.worldBound;
            Check("v3.scrolled-target-viewport",element.worldBound.width>0&&element.worldBound.height>0&&element.worldBound.xMin>=viewport.xMin-3&&element.worldBound.xMax<=viewport.xMax+3&&element.worldBound.yMin>=viewport.yMin-3&&element.worldBound.yMax<=viewport.yMax+3,evidence+" viewport="+viewport);
        }

        private IEnumerator V3Capture(string name)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(_directory,name+".png"));
            yield return new WaitForEndOfFrame();yield return Settle();
        }
        private IEnumerator V3ScrollTo(VisualElement element)
        {
            if(element!=null)element.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(element);
            yield return Settle();yield return Settle();
        }
        private IEnumerator V3ExpandController()
        {
            foreach(float text in new[]{1f,1.25f,1.5f,2f})
            {
                LocalStore.Profile.settings.textScale=text;LocalStore.Save();_app.ChangeWorkspace("Aerial");_app.PreparePreview(Page.Controller);yield return Settle();yield return Settle();
                var foldout=_root.Query<Foldout>().ToList().FirstOrDefault(f=>f.text=="Advanced Input Mapping");
                string stage="Controller-expanded Text="+text;
                Check("v3.controller-foldout-exists",foldout!=null,stage);if(foldout==null)continue;
                Check("v3.controller-foldout-initially-closed",!foldout.value,stage);
                var toggle=foldout.Q<Toggle>();Check("v3.controller-foldout-keyboard-target",toggle!=null,stage);
                if(toggle!=null){yield return V3ScrollTo(toggle);toggle.Focus();yield return KeyPress(Key.Space);}
                Check("v3.controller-foldout-space-expands",foldout.value,stage+" "+FocusEvidence());if(!foldout.value)continue;
                var content=foldout.contentContainer;
                var targets=content.Query<TextElement>().ToList().Where(e=>e.text=="Deadzone"||e.text=="Reverse throttle"||e.text=="Reverse yaw"||e.text=="Reverse pitch"||e.text=="Reverse roll"||e.ClassListContains("unity-text-element--inner-input-field-component")).ToList();
                Check("v3.controller-expanded-six-text-targets",targets.Count==6,stage+" count="+targets.Count);
                for(int i=0;i<targets.Count;i++)
                {
                    var target=targets[i];yield return V3ScrollTo(target);
                    V3AuditTarget(target,stage+" target="+i,target.ClassListContains("unity-text-element--inner-input-field-component"));
                    yield return V3Capture("controller-expanded-text"+Mathf.RoundToInt(text*100)+"-target"+i);
                }
            }
        }
        private IEnumerator V3ScrollNumericAndRate()
        {
            foreach(float text in new[]{1f,1.25f,1.5f,2f})
            {
                LocalStore.Profile.settings.textScale=text;LocalStore.Save();_app.ChangeWorkspace("FPV");_app.PreparePreview(Page.Tuning);yield return Settle();yield return Settle();
                var inputs=_root.Query<TextElement>().ToList().Where(e=>e.ClassListContains("unity-text-element--inner-input-field-component")&&V2Displayed(e)).ToList();
                Check("v3.tuning-five-numeric-targets",inputs.Count==5,"Text="+text+" count="+inputs.Count);
                for(int i=0;i<inputs.Count;i++)
                {
                    yield return V3ScrollTo(inputs[i]);V3AuditTarget(inputs[i],"Tuning Text="+text+" target="+i,true);
                    yield return V3Capture("tuning-numeric-text"+Mathf.RoundToInt(text*100)+"-target"+i);
                }
                // Reuse the exact v2 synthetic replay setup; preserve its intrinsic width failures.
                _app.ChangeWorkspace("Aerial");
                var record=new SessionRecord{id="space-v3-rate-fixture",title="QA synthetic rate text diagnostic — NOT pilot achievement",workspace="Aerial",environment="Pacific Coast",aircraft="DJI Mini 4 Pro",duration=20,config=new FlightConfig(),passed=false};
                record.frames.Add(new TelemetryFrame{time=0,position=new Vector3(0,8,10),battery=100});record.frames.Add(new TelemetryFrame{time=20,position=new Vector3(10,12,25),battery=99});_app.BeginReplay(record);yield return Settle();yield return Settle();
                var rate=_root.Query<TextElement>().ToList().FirstOrDefault(e=>e.GetType().Name=="PopupTextElement"&&e.text=="1.0x");
                yield return V3ScrollTo(rate);V3AuditTarget(rate,"Replay rate Text="+text);yield return V3Capture("replay-rate-text"+Mathf.RoundToInt(text*100));
            }
        }
        private IEnumerator V3HeldArrowRaw()
        {
            foreach(Key key in new[]{Key.UpArrow,Key.DownArrow,Key.LeftArrow,Key.RightArrow})
            {
                yield return FreshFlight();InputSystem.QueueStateEvent(_keyboard,new KeyboardState());InputSystem.QueueStateEvent(_gamepad,new GamepadState());yield return Settle();
                var before=FlightInput.Raw();var focus=_root.panel.focusController.focusedElement;var record=_app.Simulator.Record;
                int axis=key==Key.UpArrow||key==Key.DownArrow?2:3;float expected=key==Key.UpArrow||key==Key.RightArrow?1:-1;
                Check("v3.raw-arrow-neutral-before",before.sqrMagnitude<.001f,key+" before="+before);
                InputSystem.QueueStateEvent(_keyboard,new KeyboardState(key));yield return null;yield return null;yield return Settle();
                var held=FlightInput.Raw();var read=FlightInput.Read();
                Check("v3.raw-arrow-held-flight-axis",Mathf.Abs(held[axis]-expected)<.001f,key+" before="+before+" held="+held+" read="+read);
                Check("v3.raw-arrow-held-other-axes-neutral",Enumerable.Range(0,4).Where(i=>i!=axis).All(i=>Mathf.Abs(held[i])<.001f),key+" held="+held);
                Check("v3.raw-arrow-held-read-not-suppressed",Mathf.Sign(read[axis])==expected&&Mathf.Abs(read[axis])>.01f,key+" read="+read);
                Check("v3.raw-arrow-reaches-simulator-input",Mathf.Sign(_app.Simulator.CurrentInput[axis])==expected&&Mathf.Abs(_app.Simulator.CurrentInput[axis])>.01f,key+" simulator="+_app.Simulator.CurrentInput);
                Check("v3.raw-arrow-held-ui-focus-unchanged",_root.panel.focusController.focusedElement==focus,key+" "+FocusEvidence());
                Check("v3.raw-arrow-held-session-still-running",_app.CurrentPage==Page.Flight&&_app.Simulator.Running&&!_app.Simulator.Paused&&!_app.Simulator.ReturningHome&&_app.Simulator.Record==record,key+" "+FocusEvidence());
                InputSystem.QueueStateEvent(_keyboard,new KeyboardState());yield return Settle();
                Check("v3.raw-arrow-neutral-after-release",FlightInput.Raw().sqrMagnitude<.001f,key+" after="+FlightInput.Raw());
                yield return SpaceMustNotAct("v3-raw-held-arrow-released-"+key);
            }
        }
    }
}
