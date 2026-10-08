using System.Collections.Generic;
using AeroSim.Domain;
using AeroSim.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace AeroSim
{
    public sealed partial class AeroSimApplication
    {
        private readonly List<RenderTexture> _hangarPreviews=new();

        private VisualElement BuildHangarPage()
        {
            var selected=AircraftCatalog.Selected;
            var s=Canvas("Aircraft / Hangar","Your aircraft. Your next flight.",
                "Four DJI-inspired visual profiles and two generic trainers. Original materials and reconstructed shapes — not official DJI assets.");
            var summary=Panel("Selected · "+selected.Name,"Selection is saved on this device and used by Free Flight, Academy, Challenges and Missions.");
            summary.Add(Row(ActionButton("Fly Selected Aircraft",()=>ShowPage(Page.FreeFlight),true),
                ActionButton("Open Tuning",()=>ShowPage(Page.Tuning))));
            s.Add(summary);
            var grid=Element("product-grid","aircraft-grid");
            foreach(var profile in AircraftCatalog.All)
            {
                var p=Panel(profile.Name,profile.Category);
                p.AddToClassList("aircraft-card");p.name="aircraft-card-"+profile.Id;
                if(profile.Id==selected.Id)p.AddToClassList("aircraft-card-selected");
                var preview=RenderAircraftPreview(profile);_hangarPreviews.Add(preview);
                var image=new Image{image=preview,scaleMode=ScaleMode.ScaleToFit};image.AddToClassList("aircraft-preview");p.Add(image);
                p.Add(Text(profile.Description,"wrap","aircraft-description"));
                p.Add(Text(profile.Reference,"wrap","muted"));
                var select=ActionButton("Select "+profile.Name.Replace("DJI ",""),()=>{
                    LocalStore.Profile.selectedAircraft=profile.Id;LocalStore.Save();ShowPage(Page.Hangar);
                },true);
                if(profile.Id==selected.Id){select.text="Selected · "+profile.Name.Replace("DJI ","");select.SetEnabled(false);}
                p.Add(select);grid.Add(p);
            }
            s.Add(grid);
            s.Add(Text("Visual approximation · Shared simplified training physics · No real aircraft connection · Not affiliated with or endorsed by DJI.","wrap","muted"));
            return s;
        }

        // Same runtime model as flight and replay; previews are not stock product photos.
        public RenderTexture RenderAircraftPreview(AircraftProfile profile)
        {
            const int layer=30;
            var stage=new GameObject("Aircraft preview stage"){layer=layer};stage.transform.position=new Vector3(8000,0,0);
            var aircraft=new GameObject("Preview aircraft");aircraft.transform.SetParent(stage.transform,false);
            aircraft.AddComponent<AircraftVisualModel>().Initialize(profile);
            foreach(var node in aircraft.GetComponentsInChildren<Transform>())node.gameObject.layer=layer;
            var lamp=new GameObject("Preview soft key");lamp.transform.SetParent(stage.transform,false);
            var light=lamp.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.8f;light.color=new Color(1,.95f,.88f);
            light.cullingMask=1<<layer;light.transform.rotation=Quaternion.Euler(38,-35,0);
            var cameraHost=new GameObject("Preview camera");cameraHost.transform.SetParent(stage.transform,false);
            var camera=cameraHost.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<layer;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.024f,.067f,.10f);
            camera.fieldOfView=32;camera.nearClipPlane=.008f;camera.farClipPlane=4;
            camera.transform.position=stage.transform.position+new Vector3(1.45f,1.08f,1.78f)*profile.VisualScale;
            camera.transform.LookAt(stage.transform.position+Vector3.up*.02f*profile.VisualScale);
            var mode=RenderSettings.ambientMode;var sky=RenderSettings.ambientSkyColor;var equator=RenderSettings.ambientEquatorColor;
            var ground=RenderSettings.ambientGroundColor;bool fog=RenderSettings.fog;
            var rt=new RenderTexture(768,432,24,RenderTextureFormat.ARGB32){antiAliasing=2};rt.Create();
            try
            {
                RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Trilight;
                RenderSettings.ambientSkyColor=new Color(.58f,.66f,.75f);RenderSettings.ambientEquatorColor=new Color(.25f,.29f,.34f);
                RenderSettings.ambientGroundColor=new Color(.10f,.12f,.15f);camera.targetTexture=rt;camera.Render();
            }
            finally
            {
                camera.targetTexture=null;RenderSettings.fog=fog;RenderSettings.ambientMode=mode;
                RenderSettings.ambientSkyColor=sky;RenderSettings.ambientEquatorColor=equator;RenderSettings.ambientGroundColor=ground;
                stage.SetActive(false);Destroy(stage);
            }
            return rt;
        }

        private void ReleaseHangarPreviews()
        {
            foreach(var preview in _hangarPreviews)if(preview!=null){preview.Release();Destroy(preview);}
            _hangarPreviews.Clear();
        }
    }
}
