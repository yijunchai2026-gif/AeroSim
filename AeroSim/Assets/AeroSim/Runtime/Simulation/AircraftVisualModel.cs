using System.Collections.Generic;
using AeroSim.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroSim.Simulation
{
    // Shared by the hangar preview, gameplay and replay. No physics/mission or external network calls.
    public sealed class AircraftVisualModel : MonoBehaviour
    {
        public AircraftProfile Profile { get; private set; }
        public readonly List<Transform> Rotors=new();
        public readonly List<GameObject> MotionDiscs=new();
        private readonly List<Material> _materials=new();
        private readonly List<Mesh> _meshes=new();
        private Mesh _rounded,_ring;
        private Material _shell,_dark,_metal,_glass,_carbon,_red,_green,_blur;

        public void Initialize(AircraftProfile profile)
        {
            Profile=profile;name="Aircraft visual · "+profile.Id;
            _rounded=RoundedMesh();_ring=DuctMesh(48,.31f,.27f,.05f);
            _shell=Surface(Color.white,profile.Texture,.35f);
            _dark=Surface(new Color(.035f,.042f,.048f),null,.37f);
            _metal=Surface(new Color(.38f,.40f,.42f),null,.52f,.48f);
            _glass=Surface(new Color(.022f,.074f,.12f),null,.93f,.26f);
            _carbon=Surface(new Color(.82f,.82f,.82f),"carbon-weave-v3",.42f);
            _red=Surface(new Color(.84f,.18f,.055f));_green=Surface(new Color(.10f,.58f,.25f));
            _blur=new Material(Resources.Load<Shader>("RotorBlur"));_materials.Add(_blur);
            if(profile.Shape==AirframeShape.Ducted || profile.Shape==AirframeShape.Micro) BuildDucted(profile.Shape==AirframeShape.Micro);
            else if(profile.Shape==AirframeShape.Racer) BuildRacer();
            else BuildCameraDrone();
            transform.localScale=Vector3.one*profile.VisualScale;
        }

        private Material Surface(Color color,string texture=null,float smooth=.3f,float metal=0)
        {
            var m=new Material(Resources.Load<Shader>("TrainingSurface")){color=color,enableInstancing=true};_materials.Add(m);
            m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",metal);
            if(texture!=null)m.mainTexture=Resources.Load<Texture2D>("Art/AircraftV3/"+texture);
            return m;
        }
        private GameObject Part(string label,PrimitiveType type,Vector3 p,Vector3 scale,Material material,Transform parent=null)
        {
            var g=GameObject.CreatePrimitive(type);g.name=label;g.transform.SetParent(parent??transform,false);
            g.transform.localPosition=p;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=material;
            var collider=g.GetComponent<Collider>();collider.enabled=false;Destroy(collider);return g;
        }
        private GameObject Shape(string label,Mesh mesh,Vector3 p,Vector3 scale,Material material,Transform parent=null)
        {
            var g=new GameObject(label);g.transform.SetParent(parent??transform,false);g.transform.localPosition=p;g.transform.localScale=scale;
            g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=material;return g;
        }
        private void Rod(string label,Vector3 a,Vector3 b,float radius,Material material)
        {
            var g=Part(label,PrimitiveType.Capsule,(a+b)*.5f,new Vector3(radius,Vector3.Distance(a,b)*.5f,radius),material);
            g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
        }
        private void Lens(string label,Vector3 p,float diameter,float length)
        {
            var barrel=Part(label+" barrel",PrimitiveType.Cylinder,p,new Vector3(diameter,length,diameter),_dark);
            barrel.transform.localRotation=Quaternion.Euler(90,0,0);
            var front=Part(label+" optical glass",PrimitiveType.Cylinder,p+Vector3.forward*(length+.003f),new Vector3(diameter*.80f,.004f,diameter*.80f),_glass);
            front.transform.localRotation=Quaternion.Euler(90,0,0);
            var ring=Part(label+" optical bezel",PrimitiveType.Cylinder,p+Vector3.forward*length*.94f,new Vector3(diameter*1.04f,.004f,diameter*1.04f),_metal);
            ring.transform.localRotation=Quaternion.Euler(90,0,0);
        }

        private void BuildCameraDrone()
        {
            bool mini=Profile.Shape==AirframeShape.Mini, mavic=Profile.Shape==AirframeShape.Mavic;
            var size=mini?new Vector3(.34f,.14f,.54f):mavic?new Vector3(.43f,.19f,.74f):new Vector3(.44f,.17f,.63f);
            Shape("Sculpted folding shell",_rounded,new Vector3(0,.035f,0),size,_shell);
            Shape("Lower battery chassis",_rounded,new Vector3(0,-.042f,-.02f),Vector3.Scale(size,new Vector3(.91f,.55f,.90f)),_dark);
            Shape("Battery panel seam",_rounded,new Vector3(0,size.y*.52f-.001f,-size.z*.17f),new Vector3(size.x*.76f,.009f,size.z*.40f),_dark);
            Shape("Rear battery door",_rounded,new Vector3(0,size.y*.52f+.003f,-size.z*.17f),new Vector3(size.x*.73f,.010f,size.z*.37f),_shell);
            Part("Battery latch",PrimitiveType.Cube,new Vector3(0,size.y*.53f+.007f,-size.z*.29f),new Vector3(.045f,.008f,.023f),_dark);
            for(int side=-1;side<=1;side+=2)
            {
                for(int j=0;j<6;j++)Part("Side cooling vent",PrimitiveType.Cube,new Vector3(side*size.x*.474f,.015f,-.12f+j*.038f),new Vector3(.009f,.04f,.012f),_dark);
                var eye=new Vector3(side*size.x*.28f,.035f+size.y*.22f,size.z*.455f);
                Shape("Raised front sensor eyebrow",_rounded,eye,new Vector3(.080f,.054f,.050f),_shell);
                var sensor=Part("Front obstacle sensor",PrimitiveType.Cylinder,eye+Vector3.forward*.027f,new Vector3(.039f,.004f,.039f),_glass);
                sensor.transform.localRotation=Quaternion.Euler(90,0,0);
                Part("Rear vision sensor",PrimitiveType.Sphere,new Vector3(side*size.x*.29f,.025f,-size.z*.43f),new Vector3(.038f,.026f,.018f),_glass);
            }
            if(!mini)Shape("Forward sensor window",_rounded,new Vector3(0,.08f,size.z*.475f),new Vector3(.12f,.030f,.020f),_dark);
            var mount=new Vector3(0,-.085f,size.z*.34f);
            Rod("Gimbal neck",mount+Vector3.up*.07f,mount,.042f,_metal);
            if(mini)
            {
                Shape("Compact single-camera gimbal",_rounded,mount+new Vector3(0,-.032f,.044f),new Vector3(.135f,.11f,.12f),_metal);
                Lens("Main camera",mount+new Vector3(0,-.034f,.11f),.087f,.025f);
            }
            else if(mavic)
            {
                Shape("Three-camera gimbal",_rounded,mount+new Vector3(0,-.04f,.05f),new Vector3(.235f,.18f,.15f),_metal);
                Lens("Wide camera",mount+new Vector3(-.047f,-.07f,.126f),.10f,.027f);
                Lens("Medium tele camera",mount+new Vector3(.066f,-.080f,.128f),.060f,.026f);
                Lens("Tele camera",mount+new Vector3(.05f,.015f,.128f),.069f,.026f);
            }
            else
            {
                Shape("Stacked dual-camera gimbal",_rounded,mount+new Vector3(0,-.04f,.05f),new Vector3(.165f,.20f,.145f),_metal);
                Lens("Wide camera",mount+new Vector3(0,-.088f,.127f),.094f,.028f);
                Lens("Medium tele camera",mount+new Vector3(0,.01f,.127f),.067f,.026f);
            }
            float span=mini?.405f:mavic?.56f:.51f,front=mini?.34f:mavic?.49f:.43f,rear=mini?-.38f:mavic?-.53f:-.45f;
            for(int i=0;i<4;i++)
            {
                float side=i%2==0?-1:1;var motor=new Vector3(side*span,i<2?.002f:.027f,i<2?front:rear);
                var hinge=new Vector3(side*size.x*.36f,-.013f,i<2?size.z*.25f:-size.z*.31f);
                Rod("Folding arm",hinge,motor,mini?.050f:.068f,_shell);
                Part("Arm hinge pin",PrimitiveType.Cylinder,hinge,new Vector3(.082f,.025f,.082f),_metal);
                Rotor(motor,mini?.29f:.34f,2,i);
                Rod("Landing foot",motor-Vector3.up*.036f,motor-Vector3.up*.13f,.028f,_dark);
                Part("Navigation LED",PrimitiveType.Sphere,motor-new Vector3(0,.032f,.01f),new Vector3(.037f,.018f,.022f),i<2?_red:_green);
            }
        }

        private void BuildDucted(bool micro)
        {
            var canopy=micro?Surface(new Color(.93f,.38f,.055f),null,.34f):_shell;
            Shape("Ducted central canopy",_rounded,new Vector3(0,.05f,-.015f),new Vector3(.34f,.15f,.62f),canopy);
            Shape("Raised battery pack",_rounded,new Vector3(0,.135f,-.105f),new Vector3(.25f,.09f,.36f),_shell);
            Shape("Lower carbon structure",_rounded,new Vector3(0,-.052f,-.025f),new Vector3(.36f,.06f,.62f),_carbon);
            for(int i=0;i<4;i++)
            {
                float side=i%2==0?-1:1;var p=new Vector3(side*.30f,0,i<2?.27f:-.29f);
                Shape("Integrated prop guard",_ring,p,Vector3.one,_shell);
                Rod("Guard brace",new Vector3(side*.12f,-.02f,p.z*.7f),p,.045f,_carbon);
                Rod("Motor brace",p-new Vector3(.25f,.015f,0),p+new Vector3(.25f,-.015f,0),.022f,_carbon);
                Rotor(p,.252f,3,i);
                Part("Soft landing bumper",PrimitiveType.Sphere,p-Vector3.up*.07f,new Vector3(.085f,.045f,.085f),_dark);
            }
            Shape("Forward FPV camera housing",_rounded,new Vector3(0,.008f,.32f),new Vector3(.18f,.135f,.13f),_metal);
            Lens("Wide FPV camera",new Vector3(0,.008f,.392f),.12f,.021f);
            for(int side=-1;side<=1;side+=2)Part("Positioning camera",PrimitiveType.Sphere,new Vector3(side*.091f,-.048f,.25f),new Vector3(.03f,.02f,.025f),_glass);
            Part("Tail status indicator",PrimitiveType.Sphere,new Vector3(0,.06f,-.31f),new Vector3(.045f,.016f,.02f),_green);
        }
        private void BuildRacer()
        {
            Shape("Exposed flight stack",_rounded,new Vector3(0,.014f,0),new Vector3(.28f,.15f,.29f),_dark);
            Part("Top carbon plate",PrimitiveType.Cube,new Vector3(0,.09f,-.02f),new Vector3(.31f,.018f,.40f),_carbon);
            Shape("LiPo pack",_rounded,new Vector3(0,.17f,-.055f),new Vector3(.20f,.13f,.32f),_dark);
            Part("Battery strap",PrimitiveType.Cube,new Vector3(0,.24f,-.025f),new Vector3(.23f,.015f,.075f),_red);
            for(int i=0;i<4;i++)
            {
                var p=new Vector3(i%2==0?-.40f:.40f,0,i<2?.40f:-.40f);
                Rod("Carbon X arm",Vector3.zero,p,.064f,_carbon);Rotor(p,.30f,3,i);
            }
            Shape("FPV camera cage",_rounded,new Vector3(0,.055f,.195f),new Vector3(.18f,.15f,.14f),_red);
            Lens("FPV lens",new Vector3(0,.055f,.276f),.105f,.026f);
            Rod("Rear antenna",new Vector3(0,.09f,-.22f),new Vector3(0,.28f,-.30f),.023f,_dark);
        }
        private void Rotor(Vector3 p,float radius,int blades,int index)
        {
            Part("Brushless motor bell",PrimitiveType.Cylinder,p,new Vector3(.118f,.039f,.118f),_dark);
            Part("Motor cap",PrimitiveType.Cylinder,p+Vector3.up*.04f,new Vector3(.097f,.010f,.097f),_metal);
            var root=new GameObject("Propeller rotor "+index).transform;root.SetParent(transform,false);root.localPosition=p+Vector3.up*.059f;root.localRotation=Quaternion.Euler(0,index*33,0);
            for(int i=0;i<blades;i++)
            {
                float a=i/(float)blades*Mathf.PI*2;
                var blade=Shape("Airfoil blade",_rounded,new Vector3(Mathf.Cos(a)*radius*.54f,0,Mathf.Sin(a)*radius*.54f),new Vector3(radius*.90f,.011f,.040f),_dark,root);
                blade.transform.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg+8,3);
            }
            Part("Hub nut",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.046f,.010f,.046f),_dark,root);Rotors.Add(root);
            var disc=Shape("Motion blur disc",DiscMesh(48,radius),p+Vector3.up*.061f,Vector3.one,_blur);
            disc.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;disc.SetActive(false);MotionDiscs.Add(disc);
        }
        public void SetSpinning(bool spinning)
        {
            foreach(var disc in MotionDiscs)disc.SetActive(spinning);
        }
        private Mesh Keep(Mesh m){m.RecalculateNormals();m.RecalculateBounds();_meshes.Add(m);return m;}
        private Mesh RoundedMesh()
        {
            const int seg=32,rings=20;var v=new Vector3[(rings+1)*(seg+1)];var uv=new Vector2[v.Length];var t=new List<int>();
            float Signed(float x)=>Mathf.Sign(x)*Mathf.Pow(Mathf.Abs(x),.55f);
            for(int j=0;j<=rings;j++)for(int i=0;i<=seg;i++)
            {
                float a=i/(float)seg*Mathf.PI*2,b=j/(float)rings*Mathf.PI;int k=j*(seg+1)+i;
                var p=new Vector3(Signed(Mathf.Cos(a))*Signed(Mathf.Sin(b)),Signed(Mathf.Cos(b)),Signed(Mathf.Sin(a))*Signed(Mathf.Sin(b)))*.5f;
                p.x*=.92f-.15f*p.z;p.y+=p.z*.025f;v[k]=p;uv[k]=new Vector2(i/(float)seg,j/(float)rings);
                if(j<rings&&i<seg){t.Add(k);t.Add(k+1);t.Add(k+seg+1);t.Add(k+1);t.Add(k+seg+2);t.Add(k+seg+1);}
            }
            return Keep(new Mesh{name="Sculpted shell",vertices=v,uv=uv,triangles=t.ToArray()});
        }
        private Mesh DuctMesh(int seg,float outer,float inner,float halfHeight)
        {
            var v=new Vector3[(seg+1)*4];var uv=new Vector2[v.Length];var t=new List<int>();
            for(int i=0;i<=seg;i++)
            {
                float a=i/(float)seg*Mathf.PI*2;var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                v[i*4]=d*outer+Vector3.up*halfHeight;v[i*4+1]=d*inner+Vector3.up*halfHeight;v[i*4+2]=d*outer-Vector3.up*halfHeight;v[i*4+3]=d*inner-Vector3.up*halfHeight;
                for(int j=0;j<4;j++)uv[i*4+j]=new Vector2(i/(float)seg,j/3f);
                if(i==seg)continue;int k=i*4,n=k+4;
                void Quad(int a0,int b,int c,int d0){t.Add(a0);t.Add(c);t.Add(b);t.Add(a0);t.Add(d0);t.Add(c);}
                Quad(k,n,n+1,k+1);Quad(k+2,k+3,n+3,n+2);Quad(k,k+2,n+2,n);Quad(k+1,n+1,n+3,k+3);
            }
            return Keep(new Mesh{name="Integrated propeller duct",vertices=v,uv=uv,triangles=t.ToArray()});
        }
        private Mesh DiscMesh(int seg,float radius)
        {
            var v=new Vector3[seg+2];var uv=new Vector2[v.Length];var t=new int[seg*3];uv[0]=new Vector2(.5f,.5f);
            for(int i=0;i<=seg;i++){float a=i/(float)seg*Mathf.PI*2;v[i+1]=new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);uv[i+1]=new Vector2(.5f+Mathf.Cos(a)*.5f,.5f+Mathf.Sin(a)*.5f);if(i<seg){t[i*3]=0;t[i*3+1]=i+2;t[i*3+2]=i+1;}}
            return Keep(new Mesh{name="Rotor disc",vertices=v,uv=uv,triangles=t});
        }
        private void OnDestroy()
        {
            foreach(var material in _materials)if(material!=null)Destroy(material);
            foreach(var mesh in _meshes)if(mesh!=null)Destroy(mesh);
        }
    }
}
