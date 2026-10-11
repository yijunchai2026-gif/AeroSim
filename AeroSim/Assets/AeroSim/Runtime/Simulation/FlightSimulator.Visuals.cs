using System;
using System.Collections.Generic;
using AeroSim.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroSim.Simulation
{
    // Visual-only pass. Physics, mission targets, replay data and scoring remain unchanged.
    public sealed partial class FlightSimulator
    {
        private readonly List<Mesh> _visualMeshes = new();
        private readonly List<GameObject> _rotorDiscs = new();
        private Material _skyMaterial;
        private Material _previousSky;
        private Mesh[] _stoneVariants;
        private Mesh _branchMesh;
        private string _visualEnvironment;
        private int _environmentQuality;
        private float _rainAmount;
        private float _cloudAmount;
        private AudioClip _weatherClip;
        private bool _previousFog;
        private FogMode _previousFogMode;
        private float _previousFogDensity;
        private Color _previousFogColor, _previousAmbientSky, _previousAmbientEquator, _previousAmbientGround;
        private AmbientMode _previousAmbientMode;
        private bool _visualSettingsCaptured;
        public int EnvironmentQuality => _environmentQuality;
        public bool RainVisualActive => RainParticles != null;
        public bool RainParticlesPlaying => RainParticles != null && RainParticles.isPlaying && RainParticles.particleCount > 0;
        public bool RainVelocityCurvesConsistent
        {
            get
            {
                if (RainParticles == null) return false;
                var velocity = RainParticles.velocityOverLifetime;
                return velocity.x.mode == velocity.y.mode && velocity.y.mode == velocity.z.mode;
            }
        }
        public bool RainVelocityDirectionValid
        {
            get
            {
                if (RainParticles == null) return false;
                var velocity = RainParticles.velocityOverLifetime;
                return velocity.x.constantMin > 0 && velocity.x.constantMax > 0 &&
                    velocity.y.constantMin < 0 && velocity.y.constantMax < 0 &&
                    velocity.z.constantMin < 0 && velocity.z.constantMax < 0;
            }
        }
        private ParticleSystem RainParticles => _world != null ? _world.GetComponentInChildren<ParticleSystem>(true) : null;
        public bool EnvironmentKeywordActive(string keyword)
        {
            foreach (var material in _materials) if (material != null && material.IsKeywordEnabled(keyword)) return true;
            return false;
        }

        private Material Surface(Color tint, string texture = null, float smoothness = .25f, float metallic = 0)
        {
            var m = Material(tint);
            m.SetFloat("_Smoothness", smoothness); m.SetFloat("_Metallic", metallic);
            if (texture != null) m.mainTexture = Resources.Load<Texture2D>("Art/MaterialsV2/" + texture);
            return m;
        }

        private Material Landscape(Color tint, string texture, float tileSize, float macro = .16f, bool terrain = false)
        {
            var m = new Material(Resources.Load<Shader>("LandscapeSurface")); m.enableInstancing=true; _materials.Add(m);
            m.color = tint; m.SetTexture("_MainTex", Resources.Load<Texture2D>("Art/MaterialsV2/" + texture));
            m.SetTexture("_RockTex", Resources.Load<Texture2D>("Art/MaterialsV2/coastal-rock-v2"));
            m.SetFloat("_Scale", tileSize); m.SetFloat("_Macro", macro); m.SetFloat("_Terrain", terrain ? 1 : 0);
            m.SetFloat("_Wetness", _rainAmount); m.SetFloat("_DetailDistance", _environmentQuality >= AeroSimQuality.Ultra ? 70 : _environmentQuality >= AeroSimQuality.High ? 42 : 0);
            if (_environmentQuality >= AeroSimQuality.High) m.EnableKeyword("AEROSIM_LANDSCAPE_DETAIL");
            if (_environmentQuality >= AeroSimQuality.Ultra) m.EnableKeyword("AEROSIM_LANDSCAPE_ULTRA");
            return m;
        }

        private Mesh KeepMesh(Mesh mesh)
        {
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); _visualMeshes.Add(mesh); return mesh;
        }

        private GameObject MeshObject(string name, Mesh mesh, Vector3 position, Vector3 scale, Material material, Transform parent = null, bool collision = false)
        {
            var g = new GameObject(name); g.transform.SetParent(parent ?? _world.transform, false);
            g.transform.localPosition = position; g.transform.localScale = scale;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = g.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            if (collision) g.AddComponent<MeshCollider>().sharedMesh = mesh;
            return g;
        }

        private void BuildEnvironmentVisuals(string environment)
        {
            _world = new GameObject("Training Environment · " + environment + " · 1.0 development art"); _world.transform.SetParent(transform, false);
            _visualEnvironment = environment; _previousSky = RenderSettings.skybox;
            _previousFog=RenderSettings.fog; _previousFogMode=RenderSettings.fogMode; _previousFogDensity=RenderSettings.fogDensity; _previousFogColor=RenderSettings.fogColor;
            _previousAmbientMode=RenderSettings.ambientMode; _previousAmbientSky=RenderSettings.ambientSkyColor;
            _previousAmbientEquator=RenderSettings.ambientEquatorColor; _previousAmbientGround=RenderSettings.ambientGroundColor;
            _visualSettingsCaptured=true;
            var evening = Config.timeOfDay > 18;
            var weather = (Config.weather ?? "Clear").Trim();
            var raining = weather.Equals("Rain", StringComparison.OrdinalIgnoreCase);
            var overcast = raining || weather.Equals("Overcast", StringComparison.OrdinalIgnoreCase);
            _rainAmount = raining ? 1f : 0f;
            _cloudAmount = raining ? 1f : overcast ? .86f : .26f;
            ConfigureVisualQuality();

            // One weather state drives sky, fog, key light, materials, water, VFX and ambience.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            var clearSky = evening ? new Color(.54f,.53f,.65f) : new Color(.64f,.73f,.84f);
            RenderSettings.ambientSkyColor = Color.Lerp(clearSky, new Color(.40f,.45f,.50f), _cloudAmount*.66f);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(.43f,.46f,.45f), new Color(.31f,.34f,.35f), _cloudAmount*.7f);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(.19f,.20f,.18f), new Color(.14f,.15f,.15f), _cloudAmount);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = raining ? .0046f : overcast ? .0031f : .00115f;
            var clearFog = evening ? new Color(.65f,.58f,.56f) : new Color(.69f,.78f,.82f);
            RenderSettings.fogColor = Color.Lerp(clearFog, new Color(.46f,.52f,.56f), _cloudAmount*.78f);
            var sun = new GameObject("Weather key light").AddComponent<Light>(); sun.transform.SetParent(_world.transform, false);
            sun.type = LightType.Directional; sun.intensity = raining ? .48f : overcast ? .72f : 1.18f;
            sun.color = Color.Lerp(evening ? new Color(1,.67f,.40f) : new Color(1,.94f,.83f), new Color(.78f,.84f,.88f), _cloudAmount*.58f);
            sun.transform.rotation = Quaternion.Euler(evening ? 13 : 36,-38,0);
            sun.shadows = _environmentQuality == AeroSimQuality.Low ? LightShadows.Hard : LightShadows.Soft;
            sun.shadowStrength = raining ? .46f : overcast ? .58f : .78f; sun.shadowBias = .035f; sun.shadowNormalBias = .22f;
            _skyMaterial = new Material(Resources.Load<Shader>("Atmosphere")); _materials.Add(_skyMaterial);
            _skyMaterial.SetVector("_SunDirection", -sun.transform.forward);
            var zenith = evening ? new Color(.21f,.27f,.40f) : new Color(.19f,.42f,.65f);
            _skyMaterial.SetColor("_Zenith", Color.Lerp(zenith, new Color(.31f,.36f,.41f), _cloudAmount*.9f));
            _skyMaterial.SetColor("_Horizon", RenderSettings.fogColor);
            _skyMaterial.SetColor("_SunTint", sun.color); _skyMaterial.SetFloat("_CloudCover", _cloudAmount);
            _skyMaterial.SetFloat("_Rain", _rainAmount); _skyMaterial.SetFloat("_CloudSpeed", raining ? .012f : .004f);
            if (_environmentQuality >= AeroSimQuality.High) _skyMaterial.EnableKeyword("AEROSIM_SKY_HIGH");
            if (_environmentQuality >= AeroSimQuality.Ultra) _skyMaterial.EnableKeyword("AEROSIM_SKY_ULTRA");
            RenderSettings.skybox = _skyMaterial;

            bool desert = environment == "Desert Range", city = environment == "City Park", warehouse = environment == "Warehouse";
            var ground = Landscape(desert ? new Color(1.12f,.85f,.57f) : new Color(.91f,.99f,.87f), desert ? "coastal-rock-v2" : "meadow-grass-v2", desert ? 9 : 5, .21f, true);
            if (warehouse) ground = EnvironmentSurfaceV1(new Color(.54f,.53f,.48f),0,null,5);
            var stone = Landscape(desert ? new Color(1.03f,.75f,.53f) : new Color(.82f,.88f,.91f), "coastal-rock-v2", 4, .09f);
            var bark = Landscape(new Color(.35f,.27f,.17f), "coastal-rock-v2", 1.7f, .1f);
            var scrub = Landscape(new Color(.43f,.51f,.32f), "meadow-grass-v2", 2, .07f);
            var needles = Foliage(new Color(.74f,.85f,.75f));
            var lighterNeedles = Foliage(new Color(.92f,.95f,.78f));
            var concrete = EnvironmentSurfaceV1(new Color(.66f,.67f,.62f),0,null,3);
            var steel = EnvironmentSurfaceV1(new Color(.27f,.29f,.27f),1,null,2);
            MeshObject("Rolling terrain with flat training corridor", TerrainMesh(120, 720), Vector3.zero, Vector3.one, ground, collision:true);
            BuildLandingPad();

            _stoneVariants = new Mesh[4];
            for(int i=0;i<4;i++) _stoneVariants[i] = OrganicMesh(24,16,i+7,.32f);
            _branchMesh = NeedleCrownMesh();
            int seed=17; foreach(char c in environment) seed=unchecked(seed*31+c);
            var rng=new System.Random(seed);
            int count = _environmentQuality == AeroSimQuality.Low ? 72 : _environmentQuality == AeroSimQuality.Medium ? 128 : _environmentQuality == AeroSimQuality.High ? 190 : Application.isMobilePlatform ? 210 : 240;
            for(int i=0;i<count;i++)
            {
                float x=(float)rng.NextDouble()*540-260, z=(float)rng.NextDouble()*550-220;
                // Deterministic broad tree groups read as woodland instead of uniform random noise.
                if(!desert && !city && !warehouse && i%3!=0)
                {
                    int cluster=i%7; float angle=(float)rng.NextDouble()*Mathf.PI*2, radius=Mathf.Sqrt((float)rng.NextDouble())*72;
                    x=-215+cluster*67+Mathf.Cos(angle)*radius; z=-105+(cluster%3)*145+Mathf.Sin(angle)*radius;
                }
                // Keep the existing gate / RTH practice volume obstacle-free.
                if(Mathf.Abs(x)<46 && z>-32 && z<155 || environment=="Pacific Coast" && x < -78 || warehouse && Mathf.Abs(x)<58 && z>-35 && z<150) continue;
                float h=7+(float)rng.NextDouble()*17;
                if(desert)
                {
                    var g=MeshObject("Stratified canyon formation",_stoneVariants[i%4],new Vector3(x,GroundHeight(x,z)+h*.25f,z),new Vector3(h*1.3f,h*.85f,h),stone,collision:true);
                    g.transform.localRotation=Quaternion.Euler(0,rng.Next(360),0);
                }
                else if(city && i%4==0 && (Mathf.Abs(x)>95 || z>170)) BuildBuilding(x,z,h+12,concrete,steel);
                else BuildTree(x,z,h,bark,i%3==0?lighterNeedles:needles,rng);
            }
            if(!city && !warehouse) BuildDistantRidges(stone,ground,desert);
            if(environment=="Pacific Coast")
            {
                var water=new Material(Resources.Load<Shader>("OceanSurface")); _materials.Add(water);
                water.SetVector("_SunDirection",-sun.transform.forward); water.SetFloat("_Evening",evening?1:0);
                water.SetFloat("_Rain",_rainAmount); water.SetFloat("_Wind",Mathf.Clamp01(Config.windMph/20f));
                water.SetColor("_SkyColor",Color.Lerp(RenderSettings.fogColor,RenderSettings.ambientSkyColor,.55f));
                if(_environmentQuality>=AeroSimQuality.High) water.EnableKeyword("AEROSIM_WATER_HIGH");
                if(_environmentQuality>=AeroSimQuality.Ultra) water.EnableKeyword("AEROSIM_WATER_ULTRA");
                int waterCells=_environmentQuality==AeroSimQuality.Low?28:_environmentQuality==AeroSimQuality.Medium?48:_environmentQuality==AeroSimQuality.High?70:90;
                MeshObject("Pacific ocean · wave and Fresnel surface",FlatMesh(waterCells,780,1200),new Vector3(-470,-.25f,0),Vector3.one,water);
                for(int i=0;i<42;i++)
                {
                    float x=-85-(i%4)*4, z=i*17-320;
                    var g=MeshObject("Eroded shoreline outcrop",_stoneVariants[i%4],new Vector3(x,-2,z),new Vector3(12+i%3*5,7+i%5*2,14),stone,collision:true);
                    g.transform.localRotation=Quaternion.Euler(0,i*61,0);
                }
                BuildCoastalDetailsV1(concrete,steel);
            }
            if(warehouse)
            {
                BuildWarehouse(concrete,steel);
                BuildIndustrialDetailsV1(concrete,steel);
            }
            if(Config.professional || city)
            {
                BuildBridge(concrete,steel);
                BuildBridgeDetailsV1(concrete,steel,-sun.transform.forward,evening);
            }
            if(!desert && !warehouse) BuildGroundDetails(rng,scrub,stone);

            var cameraObject=new GameObject("Flight Camera"); cameraObject.transform.SetParent(_world.transform,false);
            View=cameraObject.AddComponent<Camera>(); View.clearFlags=CameraClearFlags.Skybox;
            View.farClipPlane=_environmentQuality==AeroSimQuality.Low?950:_environmentQuality==AeroSimQuality.Ultra?1750:1400; View.nearClipPlane=.08f;
            if(!global::AeroSim.AeroSimApplication.IsFovXrControlled) View.fieldOfView=LocalStore.GetVerticalFov(ActiveFovViewId);
            View.allowHDR=false; View.allowMSAA=_environmentQuality>AeroSimQuality.Low; cameraObject.AddComponent<AudioListener>();
            BuildWeatherVisuals(raining, seed, cameraObject.transform);
            BuildWeatherAmbience(raining, overcast, seed, cameraObject.transform);
        }

        private void ConfigureVisualQuality()
        {
            _environmentQuality = AeroSimQuality.Normalize(LocalStore.Profile.settings.quality);
            int q = _environmentQuality;
            QualitySettings.shadows=q==AeroSimQuality.Low?ShadowQuality.HardOnly:ShadowQuality.All;
            QualitySettings.shadowDistance=Application.isMobilePlatform
                ? q==AeroSimQuality.Low?38:q==AeroSimQuality.Ultra?85:65
                : q==AeroSimQuality.Ultra?180:q==AeroSimQuality.High?135:95;
            QualitySettings.shadowResolution=q==AeroSimQuality.Ultra&&!Application.isMobilePlatform?ShadowResolution.VeryHigh:
                q>=AeroSimQuality.High&&!Application.isMobilePlatform?ShadowResolution.High:ShadowResolution.Medium;
            QualitySettings.shadowCascades=q==AeroSimQuality.Ultra&&!Application.isMobilePlatform?4:q==AeroSimQuality.Low?1:2;
            QualitySettings.antiAliasing=q==AeroSimQuality.Low?0:Application.isMobilePlatform?(q==AeroSimQuality.Ultra?4:2):(q==AeroSimQuality.Ultra?8:4);
            QualitySettings.anisotropicFiltering=q==AeroSimQuality.Low?AnisotropicFiltering.Disable:AnisotropicFiltering.Enable;
            QualitySettings.lodBias=q==AeroSimQuality.Low ? .65f : q==AeroSimQuality.Medium ? 1f : q==AeroSimQuality.High ? 1.35f : 1.7f;
            QualitySettings.maximumLODLevel=q==AeroSimQuality.Low?1:0;
        }

        private void BuildWeatherVisuals(bool raining, int seed, Transform cameraRoot)
        {
            // Low is the explicit VFX-off path; lighting, wetness and fog still communicate rain.
            if (!raining || _environmentQuality == AeroSimQuality.Low) return;
            var rainObject = new GameObject("Deterministic camera-local rain");
            rainObject.transform.SetParent(cameraRoot, false); rainObject.transform.localPosition = new Vector3(0,9,8);
            var particles = rainObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true; main.duration = 2; main.startLifetime = new ParticleSystem.MinMaxCurve(.55f,.9f);
            main.startSpeed = 0; main.startSize = new ParticleSystem.MinMaxCurve(.018f,.032f);
            main.maxParticles = _environmentQuality >= AeroSimQuality.Ultra ? 1300 : _environmentQuality >= AeroSimQuality.High ? 850 : 360;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission; emission.rateOverTime = _environmentQuality >= AeroSimQuality.Ultra ? 920 : _environmentQuality >= AeroSimQuality.High ? 620 : 260;
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(25,1,24);
            var velocity = particles.velocityOverLifetime; velocity.enabled = true;
            float windDrift = Mathf.Clamp(Config.windMph*.055f,.15f,1.2f);
            velocity.x = new ParticleSystem.MinMaxCurve(windDrift,windDrift);
            velocity.y = new ParticleSystem.MinMaxCurve(-27,-20);
            velocity.z = new ParticleSystem.MinMaxCurve(-1.3f,-1.3f);
            var renderer = rainObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.lengthScale = 2.2f; renderer.velocityScale = .06f;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            var rainMaterial = new Material(Resources.Load<Shader>("RainStreak")); _materials.Add(rainMaterial);
            rainMaterial.SetFloat("_Opacity", _environmentQuality >= AeroSimQuality.Ultra ? .40f : _environmentQuality >= AeroSimQuality.High ? .34f : .24f); renderer.sharedMaterial = rainMaterial;
            particles.randomSeed = unchecked((uint)seed); particles.useAutoRandomSeed = false; particles.Play();
        }

        private void BuildWeatherAmbience(bool raining, bool overcast, int seed, Transform cameraRoot)
        {
            if ((!raining && !overcast) || LocalStore.Profile.settings.volume <= 0) return;
            const int sampleRate = 11025, sampleCount = 11025;
            var samples = new float[sampleCount]; var random = new System.Random(seed ^ 0x5f3759df);
            float low = 0;
            for (int i=0;i<sampleCount;i++)
            {
                float white=(float)random.NextDouble()*2-1;
                low=Mathf.Lerp(low,white,raining ? .16f : .035f);
                samples[i]=(low*.58f+white*(raining ? .14f : .025f))*(raining ? .34f : .12f);
            }
            _weatherClip=AudioClip.Create(raining ? "Deterministic rain ambience" : "Deterministic wind ambience",sampleCount,1,sampleRate,false);
            _weatherClip.SetData(samples,0);
            var source=cameraRoot.gameObject.AddComponent<AudioSource>(); source.clip=_weatherClip; source.loop=true; source.spatialBlend=0;
            source.volume=LocalStore.Profile.settings.volume*(raining ? .22f : .075f); source.Play();
        }

        private float GroundHeight(float x,float z)
        {
            if(_visualEnvironment=="Warehouse" || _visualEnvironment=="City Park") return 0;
            if(_visualEnvironment=="Pacific Coast" && x < -82) return -Mathf.Min(18,(-82-x)*.45f);
            float edgeX=Mathf.SmoothStep(0,1,Mathf.Clamp01((Mathf.Abs(x)-45)/45));
            float edgeZ=Mathf.SmoothStep(0,1,Mathf.Clamp01(Mathf.Max(-32-z,z-155)/55));
            float safe=Mathf.Max(edgeX,edgeZ);
            float rolling=(Mathf.PerlinNoise(x*.008f+3.4f,z*.008f+7.8f)*15+Mathf.PerlinNoise(x*.026f+9,z*.026f+2)*3-5)*safe;
            return rolling;
        }

        private Mesh TerrainMesh(int cells,float size)
        {
            var v=new Vector3[(cells+1)*(cells+1)]; var uv=new Vector2[v.Length]; var triangles=new int[cells*cells*6];
            for(int z=0;z<=cells;z++) for(int x=0;x<=cells;x++)
            {
                int i=z*(cells+1)+x; float px=(x/(float)cells-.5f)*size,pz=(z/(float)cells-.5f)*size;
                v[i]=new Vector3(px,GroundHeight(px,pz),pz); uv[i]=new Vector2(x/(float)cells,z/(float)cells);
            }
            GridTriangles(cells,triangles);
            return KeepMesh(new Mesh{name="Training terrain / 6m grid",vertices=v,uv=uv,triangles=triangles});
        }
        private Mesh FlatMesh(int cells,float width,float depth)
        {
            var v=new Vector3[(cells+1)*(cells+1)];var uv=new Vector2[v.Length];var t=new int[cells*cells*6];
            for(int z=0;z<=cells;z++)for(int x=0;x<=cells;x++){int i=z*(cells+1)+x;v[i]=new Vector3((x/(float)cells-.5f)*width,0,(z/(float)cells-.5f)*depth);uv[i]=new Vector2(x/(float)cells,z/(float)cells);}
            GridTriangles(cells,t);return KeepMesh(new Mesh{name="Wave grid",vertices=v,uv=uv,triangles=t});
        }
        private static void GridTriangles(int cells,int[] t)
        {
            for(int z=0;z<cells;z++)for(int x=0;x<cells;x++){int i=z*(cells+1)+x,k=(z*cells+x)*6;t[k]=i;t[k+1]=i+cells+1;t[k+2]=i+1;t[k+3]=i+1;t[k+4]=i+cells+1;t[k+5]=i+cells+2;}
        }

        private Mesh OrganicMesh(int segments,int rings,int seed,float noise,float exponent=1)
        {
            var v=new Vector3[(rings+1)*(segments+1)];var uv=new Vector2[v.Length];var t=new List<int>();
            for(int j=0;j<=rings;j++)for(int i=0;i<=segments;i++)
            {
                float a=i/(float)segments*Mathf.PI*2,b=j/(float)rings*Mathf.PI;
                Func<float,float> signed = n=>Mathf.Sign(n)*Mathf.Pow(Mathf.Abs(n),exponent);
                Vector3 p=new Vector3(signed(Mathf.Cos(a))*signed(Mathf.Sin(b)),signed(Mathf.Cos(b)),signed(Mathf.Sin(a))*signed(Mathf.Sin(b)));
                float vary=1+noise*(Mathf.PerlinNoise(p.x*2+seed,p.z*2+seed*.71f)-.5f)*2;
                int k=j*(segments+1)+i;v[k]=p*.5f*vary;uv[k]=new Vector2(i/(float)segments,j/(float)rings);
                if(j<rings && i<segments){t.Add(k);t.Add(k+1);t.Add(k+segments+1);t.Add(k+1);t.Add(k+segments+2);t.Add(k+segments+1);}
            }
            return KeepMesh(new Mesh{name="Organic form",vertices=v,uv=uv,triangles=t.ToArray()});
        }

        private Mesh CanopyMesh()
        {
            const int seg=18,rings=7;var v=new Vector3[(rings+1)*(seg+1)];var uv=new Vector2[v.Length];var t=new List<int>();
            for(int j=0;j<=rings;j++)for(int i=0;i<=seg;i++)
            {
                float y=j/(float)rings,a=i/(float)seg*Mathf.PI*2;
                float radius=(1-y)*(.78f+.22f*Mathf.Sin(y*19))*(1+.13f*Mathf.Sin(a*7+y*6));
                int k=j*(seg+1)+i;v[k]=new Vector3(Mathf.Cos(a)*radius*.5f,y,Mathf.Sin(a)*radius*.5f);uv[k]=new Vector2(i/(float)seg,y);
                if(j<rings && i<seg){t.Add(k);t.Add(k+seg+1);t.Add(k+1);t.Add(k+1);t.Add(k+seg+1);t.Add(k+seg+2);}
            }
            return KeepMesh(new Mesh{name="Layered pine branches",vertices=v,uv=uv,triangles=t.ToArray()});
        }

        private Material Foliage(Color tint)
        {
            var m=new Material(Resources.Load<Shader>("NeedleFoliage"));m.enableInstancing=true;_materials.Add(m);
            m.color=tint;m.mainTexture=Resources.Load<Texture2D>("Art/MaterialsV2/evergreen-needles-v2");
            m.SetFloat("_Wind",_environmentQuality==AeroSimQuality.Low?0:Mathf.Clamp01(Config.windMph/18f)); m.SetFloat("_Wetness",_rainAmount);
            if(_environmentQuality>=AeroSimQuality.High)m.EnableKeyword("AEROSIM_FOLIAGE_WIND");
            if(_environmentQuality>=AeroSimQuality.Ultra)m.EnableKeyword("AEROSIM_FOLIAGE_ULTRA"); return m;
        }
        private Mesh NeedleCrownMesh()
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
            void Card(Vector3 center,Vector3 right,Vector3 up)
            {
                int k=v.Count;v.Add(center-right-up);v.Add(center+right-up);v.Add(center+right+up);v.Add(center-right+up);
                uv.Add(new Vector2(0,0));uv.Add(new Vector2(1,0));uv.Add(new Vector2(1,1));uv.Add(new Vector2(0,1));
                t.Add(k);t.Add(k+2);t.Add(k+1);t.Add(k);t.Add(k+3);t.Add(k+2);
            }
            for(int tier=0;tier<7;tier++)for(int branch=0;branch<12;branch++)
            {
                float y=tier/7f,a=branch/12f*Mathf.PI*2+tier*.37f;
                var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var tangent=new Vector3(-radial.z,0,radial.x);
                float radius=(1-y)*.56f*(.85f+.15f*Mathf.Sin(branch*2.7f+tier));
                var center=radial*radius*.48f+Vector3.up*(y+.04f*Mathf.Sin(branch*2));
                Card(center,radial*radius*.6f,Vector3.up*(.1f-y*.05f)+tangent*radius*.22f);
                Card(center+Vector3.up*.025f,radial*radius*.6f,tangent*radius*.32f+Vector3.up*.012f);
            }
            return KeepMesh(new Mesh{name="Pine needle crown / crossed branch cards",vertices=v.ToArray(),uv=uv.ToArray(),triangles=t.ToArray()});
        }
        private void BuildTree(float x,float z,float h,Material bark,Material leaves,System.Random rng)
        {
            var root=new GameObject("Coastal pine").transform;root.SetParent(_world.transform,false);root.localPosition=new Vector3(x,GroundHeight(x,z),z);root.localRotation=Quaternion.Euler(0,rng.Next(360),0);
            Primitive("Trunk",PrimitiveType.Cylinder,new Vector3(0,h*.43f,0),new Vector3(h*.035f,h*.43f,h*.035f),bark,root);
            var crown=MeshObject("Textured pine needle crown",_branchMesh,new Vector3(0,h*.18f,0),new Vector3(h*.52f,h*.85f,h*.52f),leaves,root);
            if(_environmentQuality==AeroSimQuality.Low)crown.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }
        private void BuildDistantRidges(Material rock,Material grass,bool desert)
        {
            const int cells=100;
            var v=new Vector3[(cells+1)*(cells+1)];var uv=new Vector2[v.Length];var t=new int[cells*cells*6];
            for(int z=0;z<=cells;z++)for(int x=0;x<=cells;x++)
            {
                float px=x/(float)cells*1500-480,pz=z/(float)cells*920+270;
                float edge=Mathf.SmoothStep(0,1,Mathf.Clamp01((pz-270)/170));
                float coast=_visualEnvironment=="Pacific Coast"?Mathf.SmoothStep(0,1,Mathf.Clamp01((px+75)/150)):1;
                float broad=Mathf.PerlinNoise(px*.0035f+17,pz*.004f+6);
                float ridge=1-Mathf.Abs(Mathf.PerlinNoise(px*.011f+2,pz*.011f+9)*2-1);
                float detail=Mathf.PerlinNoise(px*.037f+5,pz*.037f+1);
                float h=(broad*broad*205+ridge*36+detail*8-8)*edge*coast;
                if(_visualEnvironment=="Pacific Coast") h=Mathf.Lerp(-18,h,coast);
                int i=z*(cells+1)+x;v[i]=new Vector3(px,h,pz);uv[i]=new Vector2(x/(float)cells,z/(float)cells);
            }
            GridTriangles(cells,t);
            MeshObject("Continuous eroded mountain skyline",KeepMesh(new Mesh{name="Horizon terrain",vertices=v,uv=uv,triangles=t}),Vector3.zero,Vector3.one,desert?rock:grass);
        }
        private void BuildGroundDetails(System.Random rng,Material vegetation,Material stone)
        {
            var tuft=CanopyMesh();
            int count=_environmentQuality==AeroSimQuality.Low?32:_environmentQuality==AeroSimQuality.Medium?76:_environmentQuality==AeroSimQuality.High?(Application.isMobilePlatform?92:145):(Application.isMobilePlatform?130:190);
            for(int i=0;i<count;i++)
            {
                float x=(float)rng.NextDouble()*150-70,z=(float)rng.NextDouble()*200-30;
                if(Mathf.Abs(x)<8 && Mathf.Abs(z)<8 || _visualEnvironment=="Pacific Coast"&&x<-76) continue;
                // Original low shrubs have no colliders. Do not scatter them through the new lake artwork.
                if((_visualEnvironment=="City Park" || Config.professional) && Mathf.Abs(x)<170 && z>18 && z<172) continue;
                float y=GroundHeight(x,z);
                if(i%5==0) MeshObject("Loose ground stone",_stoneVariants[i%4],new Vector3(x,y+.12f,z),new Vector3(.8f,.4f,.7f),stone);
                else MeshObject("Low scrub",tuft,new Vector3(x,y,z),new Vector3(.55f,.20f,.55f),vegetation);
            }
        }
        private void BuildLandingPad()
        {
            var pad=Surface(new Color(.18f,.23f,.25f),null,.36f);var white=Surface(new Color(.90f,.89f,.80f));var amber=Surface(new Color(.92f,.57f,.18f));
            Primitive("Rubber landing pad",PrimitiveType.Cylinder,new Vector3(0,.025f,0),new Vector3(7,.022f,7),pad,null,false);
            var ring=RingMesh(64,3.1f,3.25f);MeshObject("Safety orange pad rim",ring,new Vector3(0,.052f,0),Vector3.one,amber);
            Primitive("H left stroke",PrimitiveType.Cube,new Vector3(-.85f,.054f,0),new Vector3(.22f,.008f,2.4f),white,null,false);
            Primitive("H right stroke",PrimitiveType.Cube,new Vector3(.85f,.054f,0),new Vector3(.22f,.008f,2.4f),white,null,false);
            Primitive("H center stroke",PrimitiveType.Cube,new Vector3(0,.054f,0),new Vector3(1.8f,.008f,.22f),white,null,false);
        }
        private Mesh RingMesh(int segments,float inner,float outer)
        {
            var v=new Vector3[(segments+1)*2];var uv=new Vector2[v.Length];var t=new int[segments*6];
            for(int i=0;i<=segments;i++){float a=i/(float)segments*Mathf.PI*2;Vector3 p=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));v[i*2]=p*inner;v[i*2+1]=p*outer;uv[i*2]=new Vector2(.5f,.5f)+new Vector2(p.x,p.z)*inner/(outer*2);uv[i*2+1]=new Vector2(.5f,.5f)+new Vector2(p.x,p.z)*.5f;if(i<segments){int k=i*6,n=i*2;t[k]=n;t[k+1]=n+2;t[k+2]=n+1;t[k+3]=n+2;t[k+4]=n+3;t[k+5]=n+1;}}
            return KeepMesh(new Mesh{name="Ring",vertices=v,uv=uv,triangles=t});
        }

        private void BuildBuilding(float x,float z,float h,Material concrete,Material steel)
        {
            float y=GroundHeight(x,z);float width=14;var glass=Surface(new Color(.24f,.36f,.42f),null,.8f,.2f);
            Primitive("Office block",PrimitiveType.Cube,new Vector3(x,y+h*.5f,z),new Vector3(width,h,16),concrete);
            for(int j=0;j<Mathf.FloorToInt(h/4);j++)
            {
                Primitive("Window ribbon",PrimitiveType.Cube,new Vector3(x,y+2+j*4,z-8.02f),new Vector3(12,1.8f,.06f),glass,null,false);
                Primitive("Window ribbon",PrimitiveType.Cube,new Vector3(x-7.02f,y+2+j*4,z),new Vector3(.06f,1.8f,14),glass,null,false);
            }
            Primitive("Roof trim",PrimitiveType.Cube,new Vector3(x,y+h,z),new Vector3(14.5f,.4f,16.5f),steel,null,false);
            Primitive("Rooftop plant",PrimitiveType.Cube,new Vector3(x+3,y+h+1,z+2),new Vector3(4,2,4),steel,null,false);
        }
        private void BuildBridge(Material concrete,Material steel)
        {
            var asphalt=Landscape(new Color(.27f,.28f,.28f),"coastal-rock-v2",4,.04f);var stripe=Surface(new Color(.90f,.85f,.61f));
            Primitive("Bridge structure",PrimitiveType.Cube,new Vector3(0,24,55),new Vector3(110,3,14),concrete);
            Primitive("Bridge asphalt",PrimitiveType.Cube,new Vector3(0,25.52f,55),new Vector3(110,.06f,12.4f),asphalt);
            for(int i=0;i<4;i++)
            {
                float x=i*27-40;Primitive("Concrete pier",PrimitiveType.Cube,new Vector3(x,10,55),new Vector3(5,20,6),concrete);
                Primitive("Pier cap",PrimitiveType.Cube,new Vector3(x,21.5f,55),new Vector3(9,2,11),concrete);
            }
            for(int i=0;i<29;i++)
            {
                float x=-54+i*3.8f;for(int side=-1;side<=1;side+=2)Primitive("Guardrail post",PrimitiveType.Cube,new Vector3(x,26.1f,55+side*6.6f),new Vector3(.16f,1.3f,.16f),steel,null,false);
                if(i%2==0)Primitive("Road center dash",PrimitiveType.Cube,new Vector3(x,25.57f,55),new Vector3(2.8f,.01f,.14f),stripe,null,false);
            }
            for(int side=-1;side<=1;side+=2){Primitive("Steel guardrail",PrimitiveType.Cube,new Vector3(0,26.7f,55+side*6.6f),new Vector3(110,.18f,.18f),steel,null,false);Primitive("Deck fascia",PrimitiveType.Cube,new Vector3(0,24.7f,55+side*7.05f),new Vector3(110,.45f,.10f),steel,null,false);}
        }
        private void BuildWarehouse(Material concrete,Material steel)
        {
            Primitive("Warehouse slab",PrimitiveType.Cube,new Vector3(0,.05f,55),new Vector3(90,.1f,160),concrete);
            Primitive("Left wall",PrimitiveType.Cube,new Vector3(-45,15,55),new Vector3(1,30,160),concrete);
            Primitive("Right wall",PrimitiveType.Cube,new Vector3(45,15,55),new Vector3(1,30,160),concrete);
            var orange=Surface(new Color(.68f,.28f,.10f),null,.28f,.25f);var white=Surface(new Color(.82f,.80f,.69f));
            for(int i=0;i<8;i++)
            {
                Primitive("Steel roof truss",PrimitiveType.Cube,new Vector3(0,28,i*20-10),new Vector3(90,.5f,.5f),steel);
                Primitive("Shipping container",PrimitiveType.Cube,new Vector3(i%2==0?-25:25,3,i*18+10),new Vector3(12,6,8),orange);
                for(int j=0;j<12;j++)Primitive("Container corrugation",PrimitiveType.Cube,new Vector3((i%2==0?-25:25)-5.5f+j,3,i*18+5.96f),new Vector3(.12f,5.8f,.12f),steel,null,false);
                Primitive("Walkway marking",PrimitiveType.Cube,new Vector3(-10,.12f,i*20),new Vector3(.10f,.02f,16),white,null,false);
            }
        }

        private void BuildAircraftVisuals()
        {
            var model=_visual.gameObject.AddComponent<AircraftVisualModel>();
            model.Initialize(AircraftCatalog.Resolve(Config.aircraft));
            _rotors.Clear();_rotors.AddRange(model.Rotors);
            _rotorDiscs.Clear();_rotorDiscs.AddRange(model.MotionDiscs);
        }

        private void ReleaseVisualResources()
        {
            if(_visualSettingsCaptured)
            {
                if(RenderSettings.skybox==_skyMaterial) RenderSettings.skybox=_previousSky;
                RenderSettings.fog=_previousFog; RenderSettings.fogMode=_previousFogMode; RenderSettings.fogDensity=_previousFogDensity; RenderSettings.fogColor=_previousFogColor;
                RenderSettings.ambientMode=_previousAmbientMode; RenderSettings.ambientSkyColor=_previousAmbientSky;
                RenderSettings.ambientEquatorColor=_previousAmbientEquator; RenderSettings.ambientGroundColor=_previousAmbientGround;
                _visualSettingsCaptured=false;
            }
            if(_weatherClip!=null)Destroy(_weatherClip); _weatherClip=null;
            _skyMaterial=null; _rotorDiscs.Clear();
            foreach(var mesh in _visualMeshes)if(mesh!=null)Destroy(mesh);_visualMeshes.Clear();
        }
    }
}
