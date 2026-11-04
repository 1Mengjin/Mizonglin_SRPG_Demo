using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zilong.Demo
{
    public sealed partial class BattleDemo : MonoBehaviour
    {
        public BattleState State { get; private set; }
        public BattleScenario scenario;
        readonly Color ink = new Color(.07f,.10f,.12f,.96f);
        readonly Color jade = new Color(.24f,.83f,.74f);
        readonly Color gold = new Color(.87f,.73f,.44f);
        readonly Color red = new Color(.83f,.35f,.31f);
        readonly Dictionary<Vector2Int, Renderer> tiles = new Dictionary<Vector2Int, Renderer>();
        readonly Dictionary<Vector2Int, GameObject> obstacles = new Dictionary<Vector2Int, GameObject>();
        readonly Dictionary<Vector2Int, GameObject> hazardMarks = new Dictionary<Vector2Int, GameObject>();
        Transform goalRing;
        readonly Dictionary<DemoUnit, Transform> figures = new Dictionary<DemoUnit, Transform>();
        readonly Dictionary<DemoUnit, Text> labels = new Dictionary<DemoUnit, Text>();
        readonly List<Material> materials = new List<Material>();
        Camera view;
        Canvas canvas;
        Camera HudCamera => canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Font font;
        Text count, toast, phaseBanner;
        Button wait, cancel, endTurn, restart;
        RectTransform modal;
        Transform boardRoot, unitRoot, selectionRing, destinationRing;
        LineRenderer route;
        bool busy;
        float toastUntil;
        Vector2Int? hover;
        List<Vector2Int> walkingPath = new List<Vector2Int>();
        int walkingIndex;
        Vector3 cameraFocus, cameraTarget;
        Vector2 rightPress, rightPrevious;
        bool rightHeld, rightDragging;
        float rightPressedAt;
        const float TacticalViewSize = 2.6f;
        readonly HashSet<Vector2Int> movementBorder = new HashSet<Vector2Int>();

        void Awake()
        {
            if (editorPreviewRoot != null) editorPreviewRoot.SetActive(false);
#if ZILONG_STAGE_THREE_PLAYER
            stageThree = stageTwo = true;
#endif
            State = new BattleState();
            stageTwo = stageThree = true;
            if (ForestEnabled) InitializeForest(buildingPreview);
            else {
            var authoredMap = scenario != null ? scenario.map : LevelMap.Default();
            var authoredFighters = scenario != null ? scenario.CopyFighters() : FighterBook.Defaults();
            State.Reset(authoredMap.Clone());
            authoredFighters.Apply(State, authoredMap);
            hazardPercent = authoredMap.hazardPercent;
            }
            // Use an installed CJK font; no proprietary font file is redistributed.
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "Arial Unicode MS", "Arial" }, 24);
            MakeWorld(); MakeUI(); ResetFigures(); Refresh();
            if (!buildingPreview) Notify(ForestEnabled ? "迷踪林 除源清雾" : State.hasGoal?"我方先行 十二回合内抵达目标":"地块结构试玩 瘴气及根须为配置预览，战斗机制待接入");
        }
        void Start()
        {
            if (ForestEnabled) {
                StartCoroutine(BattleOpening());
                return;
            }
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-startupverify") >= 0)
            {
                StartCoroutine(VerifyDirectStart());
                return;
            }
            if (VerifyCombatMode)
            {
                Require(!editing && editorPanel == null && State.units[0].stats != null, "Play must start in battle without an editor");
                Require(scenario != null && State.units.Count == scenario.map.spawns.Count, "Authored scenario missing at startup");
                Require(State.units[0].cell == scenario.map.spawns[0].Cell && State.units[0].hp == scenario.fighters.units[0].hp, "Startup ignored authored data");
                Debug.Log("DIRECT_BATTLE_START_PASS");
                InitializeMap(); MakeEditorUI(); SetMode(true);
                StartCoroutine(RunCombatChecks());
                return;
            }
            StartCoroutine(BattleOpening());
        }

        IEnumerator VerifyDirectStart()
        {
            yield return null;
            var folder = Path.Combine(Application.dataPath, "../Artifacts");
            Directory.CreateDirectory(folder);
            bool valid = scenario != null && !editing && editorPanel == null && State.turn == 1
                && State.units.Count == scenario.map.spawns.Count;
            foreach (var unit in State.units)
            {
                var spawn = scenario.map.spawns.Find(item => item.instanceId == unit.id);
                var template = System.Array.Find(scenario.fighters.units, item => item.id == spawn.templateId);
                valid &= unit.cell == spawn.Cell && unit.hp == template.hp && unit.move == template.move
                    && unit.stats.attack == template.attack && unit.stats.range == template.range;
            }
            Capture(Path.Combine(folder, "direct-battle-start.png"));
            File.WriteAllText(Path.Combine(folder, "direct-start-verification.txt"), valid
                ? "PASS: player starts directly in battle with the project's authored map and unit configuration; no editing UI."
                : "FAIL: authored battle startup");
            Application.Quit(valid ? 0 : 1);
        }
        Material Mat(Color color)
        {
            var m = new Material(Resources.Load<Shader>("Whitebox")); m.color = color; materials.Add(m); return m;
        }
        GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Mat(color);
            ReleaseObject(go.GetComponent<Collider>()); return go;
        }
        Vector3 World(Vector2Int p, float h = 0) => new Vector3(p.x,h,State.layout!=null && State.layout.topLeftCoordinates ? State.Height-1-p.y : p.y);
        Vector2Int CellFromWorld(Vector3 p) => new Vector2Int(Mathf.RoundToInt(p.x),State.layout!=null && State.layout.topLeftCoordinates ? State.Height-1-Mathf.RoundToInt(p.z) : Mathf.RoundToInt(p.z));
        void MakeWorld()
        {
            view = new GameObject("Tactical camera").AddComponent<Camera>();
            view.tag = "MainCamera"; view.orthographic = true;
            var center=new Vector3((State.Width-1)*.5f,0,(State.Height-1)*.5f);
            view.orthographicSize = TacticalViewSize;
            view.transform.rotation = Quaternion.Euler(45,35,0);
            ResetCamera();
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color(.12f,.16f,.18f);
            view.nearClipPlane = .1f; view.farClipPlane = 80;
            boardRoot = new GameObject($"{State.Width} x {State.Height} board").transform;
            if (ForestEnabled) new GameObject(ForestPreviewMarker).transform.SetParent(boardRoot, false);
            Shape("Board plinth",PrimitiveType.Cube,boardRoot,center+Vector3.down*.24f,new Vector3(State.Width+.3f,.38f,State.Height+.3f),new Color(.21f,.25f,.26f));
            for (int y=0;y<State.Height;y++) for(int x=0;x<State.Width;x++)
            {
                var p = new Vector2Int(x,y);
                var tile = Shape($"Tile {x},{y}",PrimitiveType.Cube,boardRoot,World(p,-.04f),new Vector3(.95f,.1f,.95f),TileColor(p));
                tiles[p] = tile.GetComponent<Renderer>();
                obstacles[p]=Shape("Obstacle",PrimitiveType.Cube,boardRoot,World(p,.24f),new Vector3(.79f,.48f,.79f),new Color(.25f,.32f,.17f));

                hazardMarks[p]=Shape("Hazard stripe",PrimitiveType.Cube,boardRoot,World(p,.025f)+new Vector3(0,0,.33f),new Vector3(.7f,.02f,.13f),new Color(.8f,.43f,.7f));
            }
            if(State.layout!=null && State.layout.version==2)
            {
                foreach(var p in State.layout.rootCandidates)
                    Shape("根须候选 " + p,PrimitiveType.Cube,boardRoot,World(p,.06f),new Vector3(.8f,.025f,.09f),gold);
                for(int i=0;i<State.layout.patrol.Count;i++)
                {
                    var p=State.layout.patrol[i];
                    Shape("巡逻点 " + (i+1) + " " + p,PrimitiveType.Cube,boardRoot,World(p,.07f),new Vector3(.13f,.03f,.7f),new Color(.9f,.45f,.15f));
                }
            }
            goalRing=Ring("Goal marker",gold,.38f);
            SyncMapVisuals();
            unitRoot = new GameObject("Temporary units").transform;
            selectionRing = Ring("Selection",jade, .46f);
            destinationRing = Ring("Destination",gold,.43f);
            route = new GameObject("Movement path").AddComponent<LineRenderer>();
            route.sharedMaterial = Mat(gold); route.startWidth = route.endWidth = .055f;
            route.useWorldSpace = true; route.numCornerVertices = 3;
        }
        Transform Ring(string name, Color color, float radius)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.sharedMaterial = Mat(color); line.useWorldSpace = false; line.loop = true;
            line.startWidth = line.endWidth = .045f; line.positionCount = 64;
            for (int i=0;i<64;i++) { float a = i*Mathf.PI*2/64; line.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,.11f,Mathf.Sin(a)*radius)); }
            return line.transform;
        }
        Color TileColor(Vector2Int p)
        {
            if (State.blocked.Contains(p)) return LevelMap.RegionColor(MapRegion.密林);
            if (State.hasGoal && p == State.goal) return new Color(.64f,.55f,.32f);
            if (State.hazards.Contains(p)) return new Color(.44f,.30f,.43f);
            if(ForestEnabled ? ForestDisplayState.groundFog.Contains(p) : State.fog.Contains(p)) return new Color(.60f,.40f,.73f);
            if(State.layout!=null && State.layout.version==2) return LevelMap.RegionColor(State.layout.RegionAt(p))*((p.x+p.y)%2==0?1f:.95f);
            return (p.x+p.y)%2 == 0 ? new Color(.39f,.44f,.43f) : new Color(.35f,.40f,.40f);
        }
        void SyncMapVisuals()
        {
            foreach(var pair in obstacles) pair.Value.SetActive(State.blocked.Contains(pair.Key));
            foreach(var pair in hazardMarks) pair.Value.SetActive(State.hazards.Contains(pair.Key) || State.fog.Contains(pair.Key));
            if (State.Forest != null)
            {
                bool rootsActive = State.Forest.unitsById.TryGetValue(State.Forest.RootSourceInstanceId ?? "", out var source)
                    && Zilong.Forest.ForestRules.Alive(source);
                foreach (var marker in boardRoot.GetComponentsInChildren<Transform>(true))
                    if (marker.name.StartsWith("根须候选 ")) marker.gameObject.SetActive(rootsActive);
            }
            if (ForestEnabled) RefreshRewardMarkers();
            goalRing.gameObject.SetActive(State.hasGoal); goalRing.position=World(State.goal);
        }
        void ResetFigures()
        {
            foreach(var oldFigure in figures.Values)
            {
                foreach(var renderer in oldFigure.GetComponentsInChildren<Renderer>())
                {
                    var material = renderer.sharedMaterial;
                    materials.Remove(material);
                    ReleaseObject(material);
                }
                ReleaseObject(oldFigure.gameObject);
            }
            ClearCombatPresentation();
            figures.Clear(); labels.Clear();
            foreach(var u in State.units)
            {
                var root = new GameObject(u.id+" "+u.name).transform; root.SetParent(unitRoot);
                root.position = World(u.cell);
                var hit = root.gameObject.AddComponent<BoxCollider>(); hit.center=new Vector3(0,.6f,0); hit.size=new Vector3(.65f,1.3f,.65f);
                var color = u.enemy ? red : jade;
                var visual = new GameObject("Visual").transform; visual.SetParent(root, false);
                Shape("Faction base",PrimitiveType.Cylinder,visual,new Vector3(0,.08f,0),new Vector3(.65f,.06f,.65f),color);
                Shape("Whitebox body",u.id == "03" || u.id == "E3" ? PrimitiveType.Cube : PrimitiveType.Capsule,visual,new Vector3(0,.51f,0),new Vector3(.36f,.42f,.36f),new Color(.82f,.83f,.78f));
                Shape("Faction head",PrimitiveType.Sphere,visual,new Vector3(0,.97f,0),Vector3.one*.26f,color);
                figures[u] = root;
                MakeOverheadHud(u);
            }
        }
        RectTransform Box(Transform parent,string name, Vector2 anchor,Vector2 pos,Vector2 size, Color color)
        {
            var go = new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
            var rt = (RectTransform)go.transform; rt.anchorMin=rt.anchorMax=anchor; rt.pivot=anchor;
            rt.anchoredPosition=pos; rt.sizeDelta=size;
            go.GetComponent<Image>().color=color; go.GetComponent<Image>().raycastTarget=false; return rt;
        }
        Text Label(Transform parent,string name,string value,Vector2 anchor,Vector2 pos,Vector2 size,int fontSize,TextAnchor align)
        {
            var go = new GameObject(name,typeof(RectTransform),typeof(SharpText)); go.transform.SetParent(parent,false);
            var rt=(RectTransform)go.transform; rt.anchorMin=rt.anchorMax=anchor; rt.pivot=anchor; rt.anchoredPosition=pos; rt.sizeDelta=size;
            var text=go.GetComponent<Text>(); text.font=font; text.text=value; text.fontSize=fontSize; text.alignment=align;
            text.color=new Color(.92f,.92f,.86f); text.raycastTarget=false; text.supportRichText=true;
            return text;
        }
        Button Action(Transform parent,string title,Vector2 anchor,Vector2 pos,Vector2 size,Color color,UnityEngine.Events.UnityAction callback)
        {
            var rt=Box(parent,title,anchor,pos,size,color); var img=rt.GetComponent<Image>(); img.raycastTarget=true;
            var b=rt.gameObject.AddComponent<Button>(); b.targetGraphic=img;
            var colors=b.colors; colors.normalColor=Color.white; colors.highlightedColor=new Color(1.2f,1.2f,1.2f); colors.pressedColor=new Color(.65f,.78f,.77f); colors.disabledColor=new Color(.35f,.38f,.39f,.65f); b.colors=colors;
            Label(rt,"Caption",title,new Vector2(.5f,.5f),Vector2.zero,size-Vector2.one*8,23,TextAnchor.MiddleCenter);
            b.onClick.AddListener(() => { var hold = b.GetComponent<SkillHoldDescription>(); if (hold == null || !hold.SuppressClick) callback(); }); return b;
        }
        void MakeUI()
        {
            var obj=new GameObject("Battle HUD",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas=obj.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.pixelPerfect=true;
            var scaler=obj.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1600,900); scaler.matchWidthOrHeight=.5f;
            if (FindObjectOfType<EventSystem>() == null) new GameObject("UI input",typeof(EventSystem),typeof(StandaloneInputModule));
            restart=Action(canvas.transform,"重 开",new Vector2(1,1),new Vector2(-28,-28),new Vector2(130,56),ink,()=>OpenModal(true));
            endTurn=Action(canvas.transform,"结束回合",new Vector2(0,0),new Vector2(28,106),new Vector2(170,68),new Color(.40f,.20f,.18f),()=>OpenModal(false));
            count=Label(canvas.transform,"Remaining","",new Vector2(0,0),new Vector2(30,65),new Vector2(240,30),19,TextAnchor.MiddleLeft);
            MakeSelectionHud();
            if (ForestEnabled) MakeForestFeedbackHud();
            toast=Label(canvas.transform,"Feedback","",new Vector2(.5f,0),new Vector2(0,92),new Vector2(820,48),22,TextAnchor.MiddleCenter); toast.color=gold;
            if (ForestEnabled)
            {
                var roundPanel = Box(canvas.transform,"Round information background",new Vector2(.5f,1),new Vector2(0,-14),new Vector2(270,58),new Color(0,0,0,.78f));
                forestHint = Label(roundPanel,"Forest round","",new Vector2(.5f,.5f),Vector2.zero,new Vector2(246,44),26,TextAnchor.MiddleCenter);
                forestHint.color=Color.white; forestHint.fontStyle=FontStyle.Bold;
                forestForecastPanel = Box(canvas.transform,"Contextual forecast background",new Vector2(.5f,0),new Vector2(0,162),new Vector2(820,86),new Color(0,0,0,.82f));
                forestForecast = Label(forestForecastPanel,"Contextual forecast","",new Vector2(.5f,.5f),Vector2.zero,new Vector2(780,70),22,TextAnchor.MiddleCenter);
                forestForecastPanel.gameObject.SetActive(false);
            }
            phaseBanner=Label(canvas.transform,"Turn banner","",new Vector2(.5f,.5f),Vector2.zero,new Vector2(700,110),45,TextAnchor.MiddleCenter); phaseBanner.color=jade;
            MakeAnnouncementUI();
        }
        void ApplyCamera()
        {
            if(view!=null) view.transform.position=cameraFocus-view.transform.forward*30;
        }
        Vector3 ClampCameraFocus(Vector3 position)
        {
            return new Vector3(Mathf.Clamp(position.x,0,State.Width-1),0,Mathf.Clamp(position.z,0,State.Height-1));
        }
        void FocusCamera(Vector2Int cell) { cameraTarget=ClampCameraFocus(World(cell)); }
        void ResetCamera()
        {
            var focus=Vector3.zero; int allies=0;
            foreach(var u in State.units) if(!u.enemy) { focus+=World(u.cell); allies++; }
            cameraFocus=cameraTarget=ClampCameraFocus(allies>0?focus/allies:new Vector3((State.Width-1)*.5f,0,(State.Height-1)*.5f));
            rightHeld=rightDragging=false;
            ApplyCamera();
        }
        bool UpdateCameraDrag()
        {
            Vector2 pointer=Input.mousePosition;
            if(Input.GetMouseButtonDown(1) && (EventSystem.current==null || !EventSystem.current.IsPointerOverGameObject()))
            {
                rightHeld=true; rightDragging=false; rightPress=rightPrevious=pointer;
                rightPressedAt=Time.unscaledTime;
            }
            if(!rightHeld) return Input.GetMouseButton(1);
            if(Input.GetMouseButton(1))
            {
                float threshold=6*Mathf.Max(1,Screen.height/900f);
                if(!rightDragging && (pointer-rightPress).sqrMagnitude>threshold*threshold)
                {
                    rightDragging=true;
                    cameraTarget=cameraFocus; // Manual movement takes over from an unfinished focus transition.
                    rightPrevious=rightPress;
                    hover=null; PaintBoard();
                }
                if(rightDragging)
                {
                    var ground=new Plane(Vector3.up,Vector3.zero);
                    var previousRay=view.ScreenPointToRay(rightPrevious);
                    var currentRay=view.ScreenPointToRay(pointer);
                    if(ground.Raycast(previousRay,out float a) && ground.Raycast(currentRay,out float b))
                    {
                        cameraFocus=cameraTarget=ClampCameraFocus(cameraFocus+previousRay.GetPoint(a)-currentRay.GetPoint(b));
                        ApplyCamera();
                    }
                }
                rightPrevious=pointer;
                return true;
            }
            bool cancelClick=!rightDragging && Time.unscaledTime-rightPressedAt<.3f;
            rightHeld=rightDragging=false;
            if(cancelClick && !battleEnded) Cancel();
            return true;
        }
        void OnApplicationFocus(bool focused) { if(!focused) rightHeld=rightDragging=false; }
        void Update()
        {
            // A script reload during Play can tick once before Awake restores the runtime references.
            if (State == null) return;
            if (ForestEnabled) TickForestFeedback();
            if(toast.text.Length>0 && Time.unscaledTime>toastUntil) toast.text="";
            if(tutorialOverlay!=null)
            {
                if(Input.GetKeyDown(KeyCode.Escape)) CloseForestTutorial();
                rightHeld=rightDragging=false; return;
            }
            if(Input.GetKeyDown(KeyCode.Escape))
            { if(statusPopup!=null) { HideStatusDetails(); return; } if(battleEnded) return; if(modal!=null) CloseModal(); else if (ledgerPanel != null && ledgerPanel.gameObject.activeSelf) ledgerPanel.gameObject.SetActive(false); else Cancel(); return; }
            if(statusPopup!=null) { if(Input.GetMouseButtonDown(1)) HideStatusDetails(); rightHeld=rightDragging=false; return; }
            if(modal!=null) { rightHeld=rightDragging=false; return; }
            if(cinematicCamera || announcementLayer != null && announcementLayer.gameObject.activeSelf) { rightHeld=rightDragging=false; return; }
            if(UpdateCameraDrag()) return;
            if(busy) return;
            Vector2Int? cell=null;
            if(!EventSystem.current.IsPointerOverGameObject())
            {
                var ray=view.ScreenPointToRay(Input.mousePosition);
                if(Physics.Raycast(ray,out var unitHit,100))
                {
                    foreach(var pair in figures) if(pair.Value==unitHit.transform) { cell=State.selected==pair.Key && State.phase==SelectionPhase.Preview ? State.preview : pair.Key.cell; break; }
                    if(!cell.HasValue) foreach(var pair in obstacles) if(pair.Value.transform==unitHit.transform) { cell=pair.Key; break; }
                }
                if(!cell.HasValue && new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float distance))
                { var p=ray.GetPoint(distance); var c=CellFromWorld(p); if(State.Inside(c)) cell=c; }
            }
            if(cell!=hover) { hover=cell; PaintBoard(); if(editing) RefreshEditor(); }
            if(Input.GetMouseButtonDown(0) && cell.HasValue) ClickCell(cell.Value);
        }
        void LateUpdate()
        {
            if (State == null) return;
            if(tutorialOverlay!=null) return;
            AnimateRewardMarkers();
            cameraFocus=Vector3.Lerp(cameraFocus,cameraTarget,1-Mathf.Exp(-9*Time.unscaledDeltaTime));
            ApplyCamera();
            foreach(var pair in figures)
            {
                var u=pair.Key;
                if (figurePositionOverrides.TryGetValue(u, out var animatedPosition)) pair.Value.position = animatedPosition;
                else if(State.selected==u && State.phase==SelectionPhase.Preview && walkingIndex<walkingPath.Count)
                {
                    var p=World(walkingPath[walkingIndex]);
                    pair.Value.position=Vector3.MoveTowards(pair.Value.position,p,Time.deltaTime*7);
                    if(Vector3.Distance(pair.Value.position,p)<.001f) walkingIndex++;
                }
                else pair.Value.position=World(State.selected==u && State.phase==SelectionPhase.Preview ? State.preview : u.cell);
                var visual = pair.Value.Find("Visual");
                UpdateOverheadHud(u, visual != null ? visual.position : pair.Value.position);
            }
            if(State.selected!=null) selectionRing.position=World(State.selected.cell);
        }
        public void ClickCell(Vector2Int p)
        {
            if(busy || modal!=null) return;
            if(editing) { EditCell(p); return; }
            if (ForestEnabled && ForestClickSkillTarget(p)) return;
            var u=State.At(p);
            if(stageThree && CombatRules.CanBasicAttack(State,u))
            {
                if(State.phase==SelectionPhase.Selected)
                {
                    State.Preview(State.selected.cell); walkingPath.Clear(); walkingIndex=0;
                }
                StartCoroutine(PlayerAction(u)); return;
            }
            if(State.phase==SelectionPhase.Preview)
            {
                if(u!=null && u.enemy) Notify("敌人不在当前落点的攻击射程内");
                return;
            }
            if(u!=null && u!=State.selected) { State.Select(u); FocusCamera(u.cell); hover=null; Refresh(); if(ForestEnabled) StartCoroutine(CheckForestTutorials()); return; }
            if(State.Preview(p)) { walkingPath=State.Path(p); walkingIndex=1; FocusCamera(p); hover=null; Refresh(); if(ForestEnabled) StartCoroutine(CheckForestTutorials()); return; }
            if(State.selected!=null) Notify(State.blocked.Contains(p)?"障碍格 不可通行":"此格不可达 超出移动范围或被单位占用");
        }
        public void Cancel()
        {
            if (busy) return;
            if (ForestEnabled && forestTargetAction.HasValue)
            { forestTargetAction = null; forestTeleportTargetId = null; hover = null; Refresh(); return; }
            if (editing)
            { movingSpawn = null; tool = MapTool.Inspect; RefreshEditor(); PaintBoard(); return; }
            State.Cancel(); walkingPath.Clear(); walkingIndex = 0;
            if (State.selected != null) FocusCamera(State.selected.cell);
            hover = null; Refresh();
        }
        void CommitWait()
        {
            if(busy || State.selected==null || State.selected.enemy || State.selected.acted) return;
            if(State.phase==SelectionPhase.Selected)
            {
                State.Preview(State.selected.cell); walkingPath.Clear(); walkingIndex=0;
            }
            if(stageThree) { if(State.phase==SelectionPhase.Preview) StartCoroutine(PlayerAction(null)); return; }
            if(busy || !State.CommitWait()) return;
            hover=null; Refresh(); Notify("行动已确认 本回合不可再次行动");
        }
        void Notify(string message) { toast.text=message; toastUntil=Time.unscaledTime+3; }
        void Refresh()
        {
            if (ForestEnabled) RefreshForestMovementPreview();
            var u=State.selected;
            count.text=$"可行动  {State.units.FindAll(a=>!a.enemy&&!a.acted).Count} / {State.units.FindAll(a=>!a.enemy).Count}";
            if (ForestEnabled) count.text = "可行动 " + State.units.Count(a => !a.enemy && !a.acted) + "人 行动余 "
                + forestSession.State.unitsById.Values.Where(a => Zilong.Forest.ForestRules.Alive(a) && forestSession.State.GetTemplate(a.templateId).faction == Zilong.Forest.Faction.Ally).Sum(a => a.acted ? 0 : 1 + a.bonusActions) + "次";
            endTurn.interactable=restart.interactable=!busy;
            if(returnToEditor!=null) returnToEditor.interactable=!busy;
            foreach(var pair in labels)
            {
                var a=pair.Key; pair.Value.text=a.name;
                foreach(var r in figures[a].GetComponentsInChildren<Renderer>())
                    if(r.name=="Faction base" || r.name=="Faction head") r.sharedMaterial.color=a.acted?new Color(.39f,.43f,.43f):a.enemy?red:jade;
            }
            RefreshOverheadHuds();
            RefreshSelectionHud();
            if (ForestEnabled) RefreshForestHud();
            PaintBoard();
            if (ForestEnabled) { RefreshSpiritMarkers(); RefreshForestLedger(); }
            if(editing && editorPanel!=null) RefreshEditor();
            if(stageThree && !editing && State.units.Exists(a=>a.stats!=null)) RefreshCombat();
        }
        void PaintBoard()
        {
            movementBorder.Clear();
            if(!editing && (State.phase==SelectionPhase.Selected || State.phase==SelectionPhase.Preview))
            {
                // Red cells sit outside the reachable area; never disguise a legal destination as forbidden.
                State.CollectMovementBorder(movementBorder);
            }
            foreach(var pair in tiles)
            {
                var p=pair.Key; var c=TileColor(p);
                if(State.phase==SelectionPhase.Selected || State.phase==SelectionPhase.Preview)
                {
                    if(State.reachable.ContainsKey(p)) c=new Color(.30f,.66f,.64f);
                    else if(!State.blocked.Contains(p)) c=Color.Lerp(c,new Color(.29f,.22f,.22f),.4f);
                    if(movementBorder.Contains(p)) c=new Color(.83f,.35f,.31f);
                }
                if(hover==p && State.phase==SelectionPhase.Selected) c=State.reachable.ContainsKey(p)?new Color(.56f,.89f,.81f):new Color(.67f,.31f,.28f);
                if(State.phase==SelectionPhase.Preview && p==State.preview) c=gold;
                if(editing && hover==p) c=Color.Lerp(c,gold,.45f);
                if(CombatRules.CanBasicAttack(State,State.At(p))) c=red;
                pair.Value.sharedMaterial.color=c;
            }
            selectionRing.gameObject.SetActive(State.selected!=null);
            destinationRing.gameObject.SetActive(State.phase==SelectionPhase.Preview);
            destinationRing.position=World(State.preview);
            var target=State.phase==SelectionPhase.Preview?(Vector2Int?)State.preview:hover;
            var path=target.HasValue?State.Path(target.Value):new List<Vector2Int>();
            route.positionCount=path.Count; for(int i=0;i<path.Count;i++) route.SetPosition(i,World(path[i],.13f));
            if (ForestEnabled) PaintForestTargeting();
        }
        void OpenModal(bool isRestart)
        {
            if(busy || modal!=null) return;
            modal=Box(canvas.transform,"Confirm overlay",new Vector2(.5f,.5f),Vector2.zero,new Vector2(4000,4000),new Color(0,0,0,.65f));
            modal.GetComponent<Image>().raycastTarget=true;
            var panel=Box(modal,"Dialog",new Vector2(.5f,.5f),Vector2.zero,new Vector2(540,250),ink);
            Label(panel,"Message",isRestart?"重新开始？\n<size=20>恢复初始位置、行动状态与回合。</size>":stageThree?"结束我方回合？\n<size=20>未行动单位原地待机，随后敌方行动。</size>":"结束我方回合？\n<size=20>未确认的移动将取消。\n敌方阶段仅作提示，随后恢复行动次数。</size>",new Vector2(.5f,1),new Vector2(0,-28),new Vector2(500,120),28,TextAnchor.MiddleCenter);
            Action(panel,"取消",new Vector2(0,0),new Vector2(40,28),new Vector2(190,55),new Color(.25f,.28f,.29f),CloseModal);
            Action(panel,"确认",new Vector2(1,0),new Vector2(-40,28),new Vector2(190,55),new Color(.20f,.45f,.40f),()=>
            {
                CloseModal(); if(isRestart) { if(stageTwo) { StartPlay(); return; } State.Reset(); ResetFigures(); hover=null; Refresh(); Notify("已恢复初始战场"); }
                else if(stageThree) StartCoroutine(EnemyRound()); 
            });
        }
        void CloseModal() { if(modal!=null) ReleaseObject(modal.gameObject); modal=null; }
        void OnDestroy() { ClearBuffPulses(); CloseForestTutorial(); ClearRewardPopup(); if(rewardStarMesh!=null) ReleaseObject(rewardStarMesh); HideStatusDetails(); ClearAnnouncements(); if(announcementFont!=null) ReleaseObject(announcementFont); ReleaseHudAssets(); foreach(var m in materials) if(m!=null) ReleaseObject(m); if(font!=null) ReleaseObject(font); }

        void Capture(string path)
        {
            Canvas.ForceUpdateCanvases();
            var rt=new RenderTexture(Screen.width,Screen.height,24); var previous=RenderTexture.active;
            var mode=canvas.renderMode; var camera=canvas.worldCamera; float distance=canvas.planeDistance;
            Texture2D tex=null;
            try
            {
                // Camera.Render does not capture overlay canvases; bind it only for the capture.
                canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=view; canvas.planeDistance=1;
                Canvas.ForceUpdateCanvases(); view.targetTexture=rt; view.Render(); RenderTexture.active=rt;
                tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false); tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); tex.Apply();
                File.WriteAllBytes(path,tex.EncodeToPNG());
            }
            finally
            {
                view.targetTexture=null; RenderTexture.active=previous;
                canvas.renderMode=mode; canvas.worldCamera=camera; canvas.planeDistance=distance;
                Canvas.ForceUpdateCanvases(); rt.Release(); ReleaseObject(rt); if(tex!=null) ReleaseObject(tex);
            }
        }
    }
}
