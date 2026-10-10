using System;
using System.Collections.Generic;
using AeroSim.Domain;
using AeroSim.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AeroSim.Simulation
{
    public sealed class PhotoCaptureRequest
    {
        public SessionRecord Record { get; }
        public Camera Camera { get; }
        public PhotoEvidence Evidence { get; }
        public string SessionId { get; }
        public string FileName { get; }
        public Vector3 CameraPosition { get; }
        public Quaternion CameraRotation { get; }
        public float CameraFieldOfView { get; }
        public int TargetIndex { get; }
        internal FlightSimulator Owner { get; }
        internal PhotoCaptureRequest(FlightSimulator owner, SessionRecord record, Camera camera, PhotoEvidence evidence)
        {
            Owner = owner;
            Record = record; Camera = camera; Evidence = evidence;
            SessionId = record.id; FileName = evidence.fileName; TargetIndex = evidence.targetIndex;
            CameraPosition = camera.transform.position; CameraRotation = camera.transform.rotation;
            CameraFieldOfView = camera.fieldOfView;
        }
    }

    public sealed partial class FlightSimulator : MonoBehaviour
    {
        public FlightConfig Config { get; private set; }
        public SessionRecord Record { get; private set; }
        public Rigidbody Body { get; private set; }
        public Camera View { get; private set; }
        public bool Running { get; private set; }
        public bool Paused { get; private set; }
        public bool ReturningHome { get; private set; }
        public bool ObjectiveComplete { get; private set; }
        public bool ReplayMode { get; private set; }
        public bool ChaseView { get; set; } = true;
        public bool ReplayPlaying { get; set; }
        public float ReplayTime { get; private set; }
        public float ReplaySpeed { get; set; } = 1;
        public float Battery { get; private set; } = 100;
        public float Elapsed { get; private set; }
        public int GateIndex { get; private set; }
        public float Speed => Body == null ? 0 : Body.linearVelocity.magnitude;
        public string Status { get; private set; } = "Ready to Fly";
        public Vector4 CurrentInput { get; private set; }
        public Vector3 Position => Body == null ? Vector3.zero : Body.position;
        public Vector3? NextTarget => Config == null ? null : Config.professional && Config.mission != null && GateIndex < Config.mission.targets.Count ? Config.mission.targets[GateIndex] : GateIndex < _gatePositions.Count ? _gatePositions[GateIndex] : (Vector3?)null;
        public event Action<SessionRecord> Finished;
        public event Action<string> Notice;
        public event Action PauseRequested;
        public event Action<PhotoCaptureRequest> PhotoCaptureRequested;

        private GameObject _world;
        private Transform _visual;
        private readonly List<Material> _materials = new();
        private readonly List<Vector3> _gatePositions = new();
        private readonly List<GameObject> _gateVisuals = new();
        private readonly List<Transform> _rotors = new();
        private float _yaw, _hoverTime, _sampleTime, _collisionCooldown, _rotationTravel, _lastYaw;
        private bool _leftHome, _braked, _hasTakenOff, _acroInverted, _batteryWarned;
        private Vector3 _lastPosition;
        private Vector3 _pausedVelocity, _pausedAngularVelocity, _landingPoint;
        private bool _landing;
        private AudioSource _audio;
        private Material _activeGateMaterial;

        public void Begin(FlightConfig config)
        {
            CleanWorld(); Config=config; Record=new SessionRecord {id=Guid.NewGuid().ToString("N"),startedUtc=DateTime.UtcNow.ToString("O"),title=config.title,workspace=config.workspace,environment=config.environment,aircraft=config.aircraft,mode=config.mode,courseId=config.courseId};
            Record.config=JsonUtility.FromJson<FlightConfig>(JsonUtility.ToJson(config));
            Battery=config.courseId=="s02"?24:100; Elapsed=0; GateIndex=0; ObjectiveComplete=false; ReturningHome=false; ReplayMode=false; Paused=false; _landing=false;
            _yaw=0; _hoverTime=0; _sampleTime=0; _collisionCooldown=0; _rotationTravel=0; _lastYaw=0; _leftHome=false; _braked=false; _hasTakenOff=false; _acroInverted=false; _batteryWarned=false;
            BuildWorld(config.environment); BuildAircraft(); BuildGates();
            HighlightTarget();
            ChaseView=config.workspace!="FPV"; Running=true; _lastPosition=Body.position;
            Event("session","Flight started"); Status="Ready to Fly";
        }

        public void BeginReplay(SessionRecord record)
        {
            Begin(record.config??new FlightConfig {title=record.title,aircraft=record.aircraft,environment=record.environment,workspace=record.workspace,mode=record.mode});
            Record=record; ReplayMode=true; Body.isKinematic=true; ReplayTime=0; ReplayPlaying=false; Seek(0);
        }

        public void Pause(bool paused)
        {
            if(Body!=null && paused && !Paused) { _pausedVelocity=Body.linearVelocity; _pausedAngularVelocity=Body.angularVelocity; }
            Paused=paused; if (Body!=null) { Body.isKinematic=paused; if(!paused) { Body.linearVelocity=_pausedVelocity; Body.angularVelocity=_pausedAngularVelocity; } }
            if (_audio!=null) _audio.volume=paused?0:LocalStore.Profile.settings.volume*.18f;
            Status=paused?"Paused":ReturningHome?"Return-to-Home":"In Flight";
        }
        public void StartRth()
        {
            if(Paused) return;
            _landing=false; ReturningHome=true; Status="RTH · Climb"; Event("rth","Return-to-Home started"); Notice?.Invoke("Returning home. Use the sticks to take control.");
        }
        public void Land() { if(Paused) return; ReturningHome=true; _landing=true; _landingPoint=new Vector3(Position.x,.45f,Position.z); Status="Landing"; Event("landing","Landing requested"); }
        public void CancelRth() { ReturningHome=false; Status="Manual control"; Event("rth","Pilot took control"); }
        public void SignalLoss() { Event("warning","Simulated signal loss"); StartRth(); }
        public void ResetAircraft()
        {
            Body.position=new Vector3(0,1,0); Body.rotation=Quaternion.identity;
            _pausedVelocity=_pausedAngularVelocity=Vector3.zero;
            if(!Body.isKinematic) { Body.linearVelocity=Vector3.zero; Body.angularVelocity=Vector3.zero; }
            ReturningHome=false; _yaw=0; FlightInput.TouchLeft=FlightInput.TouchRight=Vector2.zero; Event("reset","Aircraft reset to home"); Notice?.Invoke("Aircraft reset.");
        }
        public bool CapturePhoto()
        {
            if (!Running || ReplayMode || Paused) return false;
            if(Position.y<2) { Notice?.Invoke("Take off above 7 ft before capturing a training photograph."); return false; }
            if (Config.professional)
            {
                var targets=Config.mission?.targets;
                if(targets==null || GateIndex>=targets.Count) { Notice?.Invoke("All inspection targets captured."); return false; }
                if(Vector3.Distance(Position,targets[GateIndex])>18) { Notice?.Invoke("Move within 59 ft of the highlighted inspection target."); return false; }
                if(Vector3.Distance(Position,targets[GateIndex])<Config.mission.safeDistance) { Notice?.Invoke("Too close: respect the mission safe-distance setting."); return false; }
            }
            if (View == null || Record == null) { Notice?.Invoke("The simulator camera is unavailable. Capture was not saved."); return false; }
            Record.photoEvidence ??= new List<PhotoEvidence>();
            int sequence = Record.photoEvidence.Count + 1;
            bool hasTarget = Config.professional && Config.mission != null;
            var target = hasTarget ? Config.mission.targets[GateIndex] : Vector3.zero;
            string captureId = Guid.NewGuid().ToString("N");
            var evidence = new PhotoEvidence
            {
                id = captureId, sessionId = Record.id, fileName = Record.id + "-photo-" + sequence + "-" + captureId + ".png",
                sequence = sequence, time = Elapsed, capturedUtc = DateTime.UtcNow.ToString("O"),
                hasTarget = hasTarget, missionId = hasTarget ? Config.mission.id : "", targetIndex = hasTarget ? GateIndex : -1,
                targetName = hasTarget && Config.mission.targetNames != null && GateIndex < Config.mission.targetNames.Count ? Config.mission.targetNames[GateIndex] : hasTarget ? "Target " + (GateIndex + 1) : "",
                aircraftPosition = Position, aircraftRotation = Body == null ? Vector3.zero : Body.rotation.eulerAngles,
                cameraPosition = View.transform.position, cameraRotation = View.transform.eulerAngles, targetPosition = target,
                targetDistance = hasTarget ? Vector3.Distance(Position, target) : 0,
                targetFacingAngle = hasTarget ? Vector3.Angle(View.transform.forward, target - View.transform.position) : 0
            };
            Record.photoEvidence.Add(evidence);
            var request = new PhotoCaptureRequest(this, Record, View, evidence);
            try
            {
                if (PhotoCaptureRequested == null) CompletePhotoCapture(request, false, "No photo evidence writer is connected.");
                else PhotoCaptureRequested.Invoke(request);
            }
            catch (Exception ex) { CompletePhotoCapture(request, false, "Capture writer failed: " + ex.Message); }
            return evidence.status == "saved";
        }
        public void CompletePhotoCapture(PhotoCaptureRequest request, bool saved, string failureReason = "")
        {
            if (request?.Evidence == null || request.Evidence.status != "pending") return;
            var evidence = request.Evidence;
            evidence.status = saved ? "saved" : "failed";
            evidence.failureReason = saved ? "" : failureReason ?? "Capture could not be saved.";
            if (saved)
            {
                evidence.width = 1280; evidence.height = 720;
                request.Record.photos++;
                request.Record.events.Add(new FlightEvent { time = evidence.time, kind = "capture", message = "Training photograph " + evidence.sequence + " saved" + (evidence.hasTarget ? " · " + evidence.targetName : ""), photoEvidenceId = evidence.id });
                if (Record == request.Record && evidence.hasTarget && GateIndex == request.TargetIndex)
                { GateIndex++; HighlightTarget(); }
            }
            else request.Record.events.Add(new FlightEvent { time = evidence.time, kind = "capture-failed", message = "Training photograph " + evidence.sequence + " failed: " + evidence.failureReason, photoEvidenceId = evidence.id });
            if (Record == request.Record) Notice?.Invoke(saved ? "Capture saved to the session timeline." : "Capture was not saved. " + evidence.failureReason);
        }
        public void Finish()
        {
            if (!Running || ReplayMode) return;
            Running=false; if (Body!=null) Body.isKinematic=true; if (_audio!=null) _audio.volume=0;
            Record.duration=Elapsed;
            int completion=ObjectiveComplete?100:Config.task=="explore"&&Elapsed>=5?90:Mathf.RoundToInt(35+Mathf.Clamp01(Elapsed/45)*20+GateIndex*5+Record.photos*5);
            Record.score=Mathf.Clamp(completion-Record.collisions*8,0,100); Record.passed=Record.score>=70 && (ObjectiveComplete || Config.task=="explore");
            Record.feedback=Record.passed?"Objective completed. Review the flight path and aim for smoother inputs.":"Keep practicing: "+Config.objective;
            Event("result",Record.passed?"Session passed":"Practice session saved");
            try { LocalStore.SaveSession(Record); } catch(Exception ex) { Debug.LogError(ex); Notice?.Invoke("Could not save this flight. Export or retry before exiting."); }
            Finished?.Invoke(Record);
        }
        public void Seek(float time)
        {
            if(Record==null || Record.frames.Count==0) return;
            ReplayTime=Mathf.Clamp(time,0,Record.duration);
            var index=Record.frames.FindIndex(f=>f.time>=ReplayTime); if(index<0) index=Record.frames.Count-1;
            var b=Record.frames[index]; var a=Record.frames[Mathf.Max(0,index-1)];
            var t=b.time>a.time?(ReplayTime-a.time)/(b.time-a.time):0;
            Body.position=Vector3.Lerp(a.position,b.position,t); Body.rotation=Quaternion.Slerp(Quaternion.Euler(a.rotation),Quaternion.Euler(b.rotation),t);
            Battery=Mathf.Lerp(a.battery,b.battery,t); CurrentInput=Vector4.Lerp(a.input,b.input,t);
        }
        public void Stop() { Running=false; ReplayMode=false; ReplayPlaying=false; CleanWorld(); }
        public void Event(string kind,string message) => Record?.events.Add(new FlightEvent {time=Elapsed,kind=kind,message=message});

        private void Update()
        {
            if(Body==null || (!Running && !ReplayMode)) return;
            if(ReplayMode)
            {
                if(ReplayPlaying) { Seek(ReplayTime+Time.unscaledDeltaTime*ReplaySpeed); if(ReplayTime>=Record.duration) ReplayPlaying=false; }
            }
            else
            {
                if(FlightInput.PausePressed) PauseRequested?.Invoke();
                if(!Paused && FlightInput.Pressed(Key.H)) StartRth();
                if(FlightInput.Pressed(Key.C)) ChaseView=!ChaseView;
                if(FlightInput.Pressed(Key.P) || (Gamepad.current?.rightShoulder.wasPressedThisFrame??false)) CapturePhoto();
                if(FlightInput.Pressed(Key.Backspace)) ResetAircraft();
                CurrentInput=FlightInput.Read();
            }
            foreach(var rotor in _rotors) rotor.Rotate(0,Paused?0:Time.deltaTime*2600,0);
            foreach(var disc in _rotorDiscs) disc.SetActive(!Paused);
            if(_audio!=null) { _audio.volume=Paused?0:LocalStore.Profile.settings.volume*.12f; _audio.pitch=.75f+Speed*.018f+Mathf.Max(0,CurrentInput.x)*.3f; }
        }

        private void FixedUpdate()
        {
            if(!Running || Paused || ReplayMode) return;
            var dt=Time.fixedDeltaTime; Elapsed+=dt; _collisionCooldown-=dt;
            Battery=Mathf.Max(0,Battery-dt*(.07f+Speed*.004f));
            if(Battery<25 && !_batteryWarned) { _batteryWarned=true; Event("warning","Low battery"); Notice?.Invoke("Low battery. Start Return-to-Home."); }
            if(Battery<10 && !ReturningHome) StartRth();
            if(Position.y>2) _hasTakenOff=true;
            if(ReturningHome)
            {
                if(CurrentInput.sqrMagnitude>.4f) CancelRth();
                else ApplyRth();
            }
            if(!ReturningHome)
            {
                if(Config.mode=="Manual") ApplyManual(dt);
                else ApplyAssisted(dt);
            }
            if(Position.y>120) { Body.AddForce(Vector3.down*15,ForceMode.Acceleration); Status="Altitude training boundary"; }
            if(new Vector2(Position.x,Position.z).magnitude>230) { Body.AddForce(-new Vector3(Position.x,0,Position.z).normalized*15,ForceMode.Acceleration); Status="Training boundary · turn back"; }
            var wind=new Vector3(Mathf.Sin(Elapsed*.23f)*.3f+.3f,0,Mathf.Cos(Elapsed*.32f)*.2f)*Config.windMph*.08f;
            Body.AddForce(wind,ForceMode.Acceleration);
            Record.distance+=Vector3.Distance(Position,_lastPosition); _lastPosition=Position; Record.maxAltitude=Mathf.Max(Record.maxAltitude,Position.y);
            _sampleTime+=dt; if(_sampleTime>=.1f) { _sampleTime=0; Record.frames.Add(new TelemetryFrame {time=Elapsed,position=Position,rotation=Body.rotation.eulerAngles,input=CurrentInput,battery=Battery,speed=Speed}); }
            CheckObjectives(dt);
            if(Config.professional && Config.mission!=null && Elapsed>=Config.mission.timeLimit) { Event("warning","Mission time limit reached"); Finish(); }
            if(!ReturningHome && !Paused && Status!="Low battery") Status=Battery<25?"Low battery":"In Flight";
        }

        private void ApplyAssisted(float dt)
        {
            var maxSpeed=Config.mode=="Sport"?24:12;
            _yaw+=CurrentInput.y*100*dt;
            Body.MoveRotation(Quaternion.Euler(0,_yaw,0));
            var target=Quaternion.Euler(0,_yaw,0)*new Vector3(CurrentInput.w*maxSpeed,CurrentInput.x*6,CurrentInput.z*maxSpeed);
            var acceleration=(target-Body.linearVelocity)*3f+Vector3.up*9.81f;
            Body.AddForce(acceleration,ForceMode.Acceleration);
            _visual.localRotation=Quaternion.Slerp(_visual.localRotation,Quaternion.Euler(CurrentInput.z*18,0,-CurrentInput.w*18),dt*7);
        }
        private void ApplyManual(float dt)
        {
            var s=LocalStore.Profile.settings;
            var rates=new Vector3(CurrentInput.z,CurrentInput.y,-CurrentInput.w)*s.rate*Mathf.Deg2Rad;
            Body.angularVelocity=Vector3.Lerp(Body.angularVelocity,Body.rotation*rates,dt*12);
            var collective=Mathf.Clamp(1/s.thrust+CurrentInput.x*.48f,0,1);
            Body.AddForce(Body.rotation*Vector3.up*(9.81f*s.thrust*collective),ForceMode.Acceleration);
            Body.AddForce(-Body.linearVelocity*.2f,ForceMode.Acceleration); _visual.localRotation=Quaternion.identity;
        }
        private void ApplyRth()
        {
            var horizontal=new Vector2(Position.x,Position.z).magnitude;
            Vector3 target;
            if(_landing) { target=_landingPoint; Status="Landing"; }
            else if(horizontal>3 && Position.y<Config.rthAltitude-1) { target=new Vector3(Position.x,Config.rthAltitude,Position.z); Status="RTH · Climb"; }
            else if(horizontal>2.5f) { target=new Vector3(0,Mathf.Max(Config.rthAltitude,Position.y),0); Status="RTH · Return"; }
            else { target=new Vector3(0,.45f,0); Status="RTH · Landing"; }
            var velocity=Vector3.ClampMagnitude((target-Position)*.8f,10);
            Body.AddForce((velocity-Body.linearVelocity)*3+Vector3.up*9.81f,ForceMode.Acceleration); Body.MoveRotation(Quaternion.Euler(0,_yaw,0));
            if((_landing || horizontal<2.5f) && Position.y<.85f)
            {
                ReturningHome=false;
                if(Config.task=="rth"&&_leftHome || Config.task=="land"&&_hasTakenOff || Config.task=="explore") ObjectiveComplete=true;
                Event("landing",_landing?"Landed at pilot-selected point":"Returned home and landed"); Notice?.Invoke("Landing complete. Finish the session to save your score.");
            }
        }

        private void CheckObjectives(float dt)
        {
            if(ObjectiveComplete) return;
            var horizontal=new Vector2(Position.x,Position.z).magnitude;
            if(horizontal>20) _leftHome=true;
            if(Speed>4.5f) _braked=true;
            var yaw=Body.rotation.eulerAngles.y; _rotationTravel+=Mathf.Abs(Mathf.DeltaAngle(_lastYaw,yaw)); _lastYaw=yaw;
            if(Vector3.Dot(Body.transform.up,Vector3.up)<-.5f) _acroInverted=true;
            bool complete=false;
            switch(Config.task)
            {
                case "hover": if(Position.y>8&&Position.y<12&&Speed<1.5f) _hoverTime+=dt; else _hoverTime=0; complete=_hoverTime>=Config.requiredTime; break;
                case "altitude": if(Position.y>19&&Position.y<23) _hoverTime+=dt; else _hoverTime=0; complete=_hoverTime>=Config.requiredTime; break;
                case "move": complete=_leftHome&&horizontal<3&&Position.y>1; break;
                case "land": complete=_hasTakenOff&&Position.y<.85f&&horizontal<4&&Speed<1; break;
                case "yaw": complete=_rotationTravel>=180&&Position.y>2; break;
                case "brake": complete=_braked&&Speed<.6f&&Position.y>2; break;
                case "photo": complete=Record.photos>=3; break;
                case "acro": complete=_acroInverted&&Vector3.Dot(Body.transform.up,Vector3.up)>.8f&&Position.y>2; break;
                case "inspection": complete=Config.mission!=null&&GateIndex>=Config.mission.targets.Count; break;
                case "gates":
                    if(GateIndex<_gatePositions.Count && Vector3.Distance(Position,_gatePositions[GateIndex])<4.5f)
                    {
                        _gateVisuals[GateIndex].SetActive(false); GateIndex++; HighlightTarget(); Record.gates=GateIndex; Event("gate","Gate "+GateIndex+" passed"); Notice?.Invoke("Gate "+GateIndex+" of "+_gatePositions.Count);
                    }
                    complete=_gatePositions.Count>0&&GateIndex>=_gatePositions.Count; break;
            }
            if(complete) { ObjectiveComplete=true; Event("objective","Training objective completed"); Notice?.Invoke("Objective complete. Finish to save your result."); }
        }

        public void Collision(float impact)
        {
            if(!Running||ReplayMode||_collisionCooldown>0) return;
            if(Position.y<1.2f && impact<2) return;
            _collisionCooldown=1.5f; Record.collisions++; Event("collision","Impact at "+impact.ToString("F1")+" m/s"); Notice?.Invoke("Collision recorded. Press Backspace or Reset to recover.");
            Gamepad.current?.SetMotorSpeeds(.15f,.25f); Invoke(nameof(StopRumble),.15f);
        }
        private void StopRumble() => Gamepad.current?.SetMotorSpeeds(0,0);

        private Material Material(Color color)
        {
            var m=new Material(Resources.Load<Shader>("TrainingSurface")); m.color=color; m.enableInstancing=true; _materials.Add(m); return m;
        }
        private GameObject Primitive(string name,PrimitiveType type,Vector3 pos,Vector3 scale,Material material,Transform parent=null,bool collider=true)
        {
            var g=GameObject.CreatePrimitive(type); g.name=name; g.transform.SetParent(parent??_world.transform,false); g.transform.localPosition=pos; g.transform.localScale=scale; g.GetComponent<Renderer>().sharedMaterial=material;
            if(!collider) Destroy(g.GetComponent<Collider>()); return g;
        }
        private void BuildWorld(string environment)
        {
            BuildEnvironmentVisuals(environment);
        }
        private void BuildAircraft()
        {
            var drone=new GameObject("Aircraft"); drone.transform.SetParent(_world.transform); drone.transform.position=new Vector3(0,.65f,0);
            Body=drone.AddComponent<Rigidbody>(); Body.mass=.8f; Body.useGravity=true; Body.interpolation=RigidbodyInterpolation.Interpolate; Body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic; Body.maxAngularVelocity=30;
            var collider=drone.AddComponent<BoxCollider>(); drone.AddComponent<CollisionRelay>().simulator=this;
            _visual=new GameObject("Aircraft Visual").transform; _visual.SetParent(drone.transform,false);
            BuildAircraftVisuals();
            var renderers=_visual.GetComponentsInChildren<Renderer>();
            var bounds=renderers[0].bounds;
            foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            collider.size=bounds.size;collider.center=drone.transform.InverseTransformPoint(bounds.center);
            _audio=drone.AddComponent<AudioSource>(); _audio.loop=true;
            var clip=AudioClip.Create("Rotor hum",22050,1,22050,false); var samples=new float[22050];
            for(int i=0;i<samples.Length;i++) samples[i]=(Mathf.Sin(i*2*Mathf.PI*110/22050)*.4f+Mathf.Sin(i*2*Mathf.PI*220/22050)*.15f)*.2f;
            clip.SetData(samples,0); _audio.clip=clip; _audio.Play();
        }
        private void BuildGates()
        {
            _gatePositions.Clear(); _gateVisuals.Clear();
            _activeGateMaterial=Material(new Color(.05f,.9f,1));
            if(Config.professional && Config.mission!=null)
            {
                var targetMaterial=Material(new Color(1,.58f,.08f));
                foreach(var p in Config.mission.targets) _gateVisuals.Add(Primitive("Inspection target",PrimitiveType.Sphere,p,new Vector3(1.3f,1.3f,1.3f),targetMaterial,null,false));
                return;
            }
            var gates=Config.task=="gates" || Config.race;
            if(!gates) return;
            var positions=new[]{new Vector3(0,8,25),new Vector3(20,10,50),new Vector3(30,12,85),new Vector3(0,10,110),new Vector3(-25,9,80),new Vector3(-15,8,40)};
            var orange=Material(new Color(1,.42f,.08f));
            foreach(var p in positions)
            {
                var gate=new GameObject("Race gate"); gate.transform.SetParent(_world.transform); gate.transform.position=p;
                Primitive("Left",PrimitiveType.Cube,new Vector3(-4,0,0),new Vector3(.5f,8,.5f),orange,gate.transform);
                Primitive("Right",PrimitiveType.Cube,new Vector3(4,0,0),new Vector3(.5f,8,.5f),orange,gate.transform);
                Primitive("Top",PrimitiveType.Cube,new Vector3(0,4,0),new Vector3(8.5f,.5f,.5f),orange,gate.transform);
                _gatePositions.Add(p); _gateVisuals.Add(gate);
            }
        }
        private void HighlightTarget()
        {
            if(GateIndex>0 && Config.professional && GateIndex-1<_gateVisuals.Count) _gateVisuals[GateIndex-1].SetActive(false);
            if(GateIndex>=_gateVisuals.Count) return;
            foreach(var renderer in _gateVisuals[GateIndex].GetComponentsInChildren<Renderer>()) renderer.sharedMaterial=_activeGateMaterial;
        }
        private void LateUpdate()
        {
            if(View==null||Body==null) return;
            if(ChaseView)
            {
                float distance=AircraftCatalog.Resolve(Config.aircraft).ChaseDistance;
                var behind=Body.rotation*new Vector3(0,distance*.43f,-distance); var desired=Position+behind;
                View.transform.position=LocalStore.Profile.settings.reducedMotion?desired:Vector3.Lerp(View.transform.position,desired,1-Mathf.Exp(-Time.unscaledDeltaTime*8));
                View.transform.LookAt(Position+Body.rotation*Vector3.forward*distance*.6f);
            }
            else
            {
                float size=AircraftCatalog.Resolve(Config.aircraft).VisualScale;
                View.transform.position=Position+Body.rotation*new Vector3(0,size*.03f,size*.60f);
                View.transform.rotation=Body.rotation*Quaternion.Euler(-LocalStore.Profile.settings.cameraAngle,0,0);
            }
        }
        private void CleanWorld()
        {
            _rotors.Clear();_visual=null;
            if(_world!=null) { _world.SetActive(false); Destroy(_world); }
            if(_audio?.clip!=null) Destroy(_audio.clip);
            foreach(var m in _materials) if(m!=null) Destroy(m); _materials.Clear(); Body=null; View=null; _audio=null;
            ReleaseVisualResources();
        }
        private void OnDestroy() { StopRumble(); CleanWorld(); }
    }
    public sealed class CollisionRelay : MonoBehaviour
    {
        public FlightSimulator simulator;
        private void OnCollisionEnter(UnityEngine.Collision collision) => simulator.Collision(collision.relativeVelocity.magnitude);
    }
}
