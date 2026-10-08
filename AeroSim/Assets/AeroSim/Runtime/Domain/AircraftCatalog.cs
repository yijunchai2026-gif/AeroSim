using System;
using System.Linq;

namespace AeroSim.Domain
{
    public enum AirframeShape { Mini, Air, Mavic, Ducted, Racer, Micro }

    public sealed class AircraftProfile
    {
        public string Id, Name, Category, Description, Texture, Reference, Source;
        public AirframeShape Shape;
        public float VisualScale, ChaseDistance;
        public bool Fpv;
        public AircraftProfile(string id,string name,string category,AirframeShape shape,string texture,float scale,float chase,bool fpv,string description,string reference,string source="")
        {
            Id=id;Name=name;Category=category;Shape=shape;Texture=texture;VisualScale=scale;ChaseDistance=chase;Fpv=fpv;Description=description;Reference=reference;Source=source;
        }
    }

    public static class AircraftCatalog
    {
        // Original artist-built visual approximations, not licensed DJI meshes or flight-controller emulation.
        public static readonly AircraftProfile[] All = {
            new("mini4","DJI Mini 4 Pro","LIGHTWEIGHT CAMERA",AirframeShape.Mini,"mini-shell-v3",.24f,2.4f,false,
                "Light-gray folding frame · single gimbal camera · compact arms", "Mini-series visual reference", "https://www.dji.com/mini-4-pro/specs"),
            new("air3s","DJI Air 3S","DUAL-CAMERA AERIAL",AirframeShape.Air,"air-shell-v3",.32f,3.1f,false,
                "Cool-gray shell · stacked dual cameras · forward sensor module", "Reference takeoff weight: 1.60 lb / 724 g", "https://www.dji.com/air-3s/specs"),
            new("mavic3p","DJI Mavic 3 Pro","TRIPLE-CAMERA FLAGSHIP",AirframeShape.Mavic,"mavic-shell-v3",.35f,3.4f,false,
                "Charcoal elongated body · three-lens gimbal · broad folding arms", "Reference takeoff weight: 2.11 lb / 958 g", "https://www.dji.com/mavic-3-pro/specs"),
            new("avata2","DJI Avata 2","GUARDED FPV",AirframeShape.Ducted,"avata-shell-v3",.24f,2.2f,true,
                "Four integrated prop guards · dark shell · front FPV camera", "Reference takeoff weight: 0.83 lb / approx. 377 g", "https://www.dji.com/avata-2/specs"),
            new("racer","5-inch Racer","GENERIC OPEN FPV",AirframeShape.Racer,"carbon-weave-v3",.28f,2.6f,true,
                "Exposed carbon X-frame · battery strap · three-blade props", "Original AeroSim training model"),
            new("micro","Micro Trainer","GENERIC PRACTICE",AirframeShape.Micro,"mini-shell-v3",.17f,1.9f,false,
                "Small guarded trainer · orange canopy · four compact ducts", "Original AeroSim training model")
        };
        public static AircraftProfile Resolve(string value)
        {
            if(string.Equals(value,"Camera Drone",StringComparison.OrdinalIgnoreCase))return All[0];
            return All.FirstOrDefault(p=>string.Equals(p.Id,value,StringComparison.OrdinalIgnoreCase)||string.Equals(p.Name,value,StringComparison.OrdinalIgnoreCase))??All[0];
        }
        public static AircraftProfile Selected => Resolve(LocalStore.Profile?.selectedAircraft);
        public static string[] Names => All.Select(p=>p.Name).ToArray();
    }
}
