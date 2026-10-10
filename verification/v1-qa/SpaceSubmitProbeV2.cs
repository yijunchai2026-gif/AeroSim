// Supplemental partial class compiled with generated, immutable-v1-derived base.
// This source does not replace SpaceSubmitProbe.cs or the frozen native matrix.
using System;
using System.IO;
using System.Linq;
using System.Collections;
using AeroSim.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace AeroSim.Qa
{
    public sealed partial class SpaceSubmitProbeV2
    {
        private static bool V2Displayed(VisualElement element)
        {
            // Include zero-sized rendered text candidates; zero geometry is a FAIL,
            // not a reason to silently omit a visible-state linebox check.
            for(var e=element;e!=null;e=e.parent)
                if(e.resolvedStyle.display==DisplayStyle.None||e.resolvedStyle.visibility==Visibility.Hidden)return false;
            return element.panel!=null;
        }
        private IEnumerator V2ArrowSpaceAndIntentionalTab()
        {
            foreach(string resumeMode in new[]{"Enter","Escape","Gamepad"})
                foreach(Key direction in new[]{Key.UpArrow,Key.DownArrow,Key.LeftArrow,Key.RightArrow})
                {
                    yield return FreshFlight();yield return KeyPress(Key.Escape);
                    if(resumeMode=="Escape")yield return KeyPress(Key.Escape);
                    else
                    {
                        var resume=FindButton("Resume Flight");Check("v2.resume-target",resume!=null,resumeMode+" "+direction);
                        if(resume!=null){resume.Focus();if(resumeMode=="Gamepad")yield return GamepadSubmit();else yield return KeyPress(Key.Enter);}
                    }
                    string stage=resumeMode+"-"+direction;
                    Check("v2.resume-running",_app.CurrentPage==Page.Flight&&_app.Simulator.Running&&!_app.Simulator.Paused,stage+" "+FocusEvidence());
                    var focus=_root.panel.focusController.focusedElement;string before=FocusEvidence();
                    yield return KeyPress(direction);
                    Check("v2.arrow-does-not-steal-focus",_root.panel.focusController.focusedElement==focus,stage+" "+before+" -> "+FocusEvidence());
                    // Actual Arrow -> Space, not the weaker v1 Space -> Arrow ordering.
                    yield return SpaceMustNotAct("v2-arrow-then-space-"+stage);
                }
            yield return FreshFlight();yield return KeyPress(Key.Tab);
            var tabTarget=_root.panel.focusController.focusedElement as Button;
            Check("v2.intentional-tab-selects-pause",tabTarget!=null&&Visible(tabTarget)&&Title(tabTarget)=="Pause",FocusEvidence());
            int submits=_submits;yield return KeyPress(Key.Space);
            Check("v2.intentional-tab-space-activates-pause",_app.CurrentPage==Page.Flight&&_app.Simulator.Paused&&_submits==submits+1,FocusEvidence()+" submitDelta="+(_submits-submits));
        }

        private void V2AuditText(string page,float textScale)
        {
            int inputs=0,labels=0;
            foreach(var element in _root.Query<TextElement>().ToList().Where(e=>V2Displayed(e)&&!string.IsNullOrWhiteSpace(e.text)))
            {
                float width=element.contentRect.width,height=element.contentRect.height,font=element.resolvedStyle.fontSize;
                bool input=element.ClassListContains("unity-text-element--inner-input-field-component");
                if(input)inputs++;else labels++;
                string evidence=page+" Text="+textScale+" "+element.GetType().Name+" "+element.text+" content="+width+"x"+height+" font="+font+" bounds="+element.worldBound;
                Check("v2.text-linebox-positive",width>0&&height>0&&font>0,evidence);
                if(width<=0||height<=0||font<=0)continue;
                // Single-line inputs can legitimately horizontally scroll while editing;
                // never excuse vertical clipping of their nested rendered TextElement.
                var required=input?
                    element.MeasureTextSize("Mg0123",0,VisualElement.MeasureMode.Undefined,0,VisualElement.MeasureMode.Undefined):
                    element.MeasureTextSize(element.text,width,element.resolvedStyle.whiteSpace==WhiteSpace.Normal?VisualElement.MeasureMode.Exactly:VisualElement.MeasureMode.Undefined,0,VisualElement.MeasureMode.Undefined);
                evidence+=" required="+required+" whiteSpace="+element.resolvedStyle.whiteSpace;
                Check(input?"v2.input-inner-linebox-readable":"v2.text-linebox-readable",height+3>=required.y,evidence);
                if(!input&&element.resolvedStyle.whiteSpace!=WhiteSpace.Normal)
                    Check("v2.singleline-text-width",width+3>=required.x,evidence);
            }
            Check("v2.text-audit-has-targets",labels>0,page+" Text="+textScale+" labels="+labels+" inputs="+inputs);
            if(page=="FPV-Tuning")Check("v2.slider-inner-text-targets",inputs>=5,page+" Text="+textScale+" inputs="+inputs);
        }

        private IEnumerator V2TextLineBoxes()
        {
            foreach(float text in new[]{1f,1.25f,1.5f,2f})
                foreach(string state in new[]{"FPV-Tuning","Aerial-Settings","Aerial-Controller","Professional-Home","Aerial-Replay"})
                {
                    LocalStore.Profile.settings.textScale=text;LocalStore.Save();
                    string workspace=state.StartsWith("FPV-")?"FPV":state.StartsWith("Professional-")?"Professional":"Aerial";
                    _app.ChangeWorkspace(workspace);
                    if(state=="Aerial-Replay")
                    {
                        // Explicit synthetic samples to test nonempty event lineboxes, not pilot achievements.
                        var record=new SessionRecord{id="space-v2-text-fixture",title="QA synthetic text-linebox record — NOT pilot achievement",workspace="Aerial",environment="Pacific Coast",aircraft="DJI Mini 4 Pro",duration=20,config=new FlightConfig(),passed=false};
                        record.frames.Add(new TelemetryFrame{time=0,position=new Vector3(0,8,10),battery=100});
                        record.frames.Add(new TelemetryFrame{time=20,position=new Vector3(10,12,25),battery=99});
                        record.events.Add(new FlightEvent{time=10,kind="qa-synthetic",message="QA synthetic: a deliberately long Return-to-Home line to verify readable wrapping at large text sizes"});
                        _app.BeginReplay(record);
                    }
                    else _app.PreparePreview(state=="FPV-Tuning"?Page.Tuning:state=="Aerial-Settings"?Page.Settings:state=="Aerial-Controller"?Page.Controller:Page.Home);
                    yield return Settle();yield return Settle();V2AuditText(state,text);
                    if(text==1f||text==2f)
                    {
                        ScreenCapture.CaptureScreenshot(Path.Combine(_directory,"text"+Mathf.RoundToInt(text*100)+"-"+state+".png"));
                        yield return new WaitForEndOfFrame();yield return Settle();
                    }
                }
        }
    }
}
