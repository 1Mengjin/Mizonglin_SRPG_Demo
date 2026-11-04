using UnityEngine;
using UnityEngine.UI;

namespace Zilong.Demo
{
    public sealed partial class BattleDemo
    {
        RectTransform selectionCard, actionWheel, skillSlots;
        Text selectedName, selectedClass, selectedHealth, selectedStats;
        Image healthFill, portraitArt;
        RawImage portraitModel;
        Sprite hudDisc;
        Texture2D hudDiscTexture;
        RenderTexture portraitTexture;
        DemoUnit portraitUnit;

        Sprite HudDisc()
        {
            if(hudDisc!=null) return hudDisc;
            const int size=512;
            hudDiscTexture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            hudDiscTexture.name="HUD circle";
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float radius=Vector2.Distance(new Vector2(x+.5f,y+.5f),Vector2.one*(size*.5f));
                pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(size*.5f-radius));
            }
            hudDiscTexture.SetPixels(pixels); hudDiscTexture.Apply();
            hudDisc=Sprite.Create(hudDiscTexture,new Rect(0,0,size,size),Vector2.one*.5f,100);
            return hudDisc;
        }

        RectTransform Disc(Transform parent,string name,Vector2 anchor,Vector2 position,float size,Color color)
        {
            var rt=Box(parent,name,anchor,position,Vector2.one*size,color);
            rt.GetComponent<Image>().sprite=HudDisc();
            return rt;
        }

        Button RoundAction(Transform parent,string title,Vector2 position,float size,Color color,UnityEngine.Events.UnityAction callback)
        {
            var button=Action(parent,title,new Vector2(.5f,.5f),position,Vector2.one*size,gold,callback);
            button.GetComponent<Image>().sprite=HudDisc();
            var inner=Disc(button.transform,"Inset",new Vector2(.5f,.5f),Vector2.zero,size-7,color);
            inner.SetAsFirstSibling();
            button.GetComponentInChildren<Text>().fontSize=size>140?38:22;
            return button;
        }

        void MakeSelectionHud()
        {
            forestSkillButtons.Clear();
            selectionCard=Box(canvas.transform,"Selected character card",new Vector2(0,1),new Vector2(24,-24),new Vector2(470,166),ink);
            if (ForestEnabled) selectionCard.sizeDelta = new Vector2(550, 374);
            selectionCard.GetComponent<Image>().color = new Color(.025f, .035f, .04f, .96f);
            selectionCard.GetComponent<Image>().raycastTarget=true;
            var frame=Disc(selectionCard,"Portrait frame",new Vector2(0,1),new Vector2(10,-10),146,gold);
            var portrait=Disc(frame,"Portrait mask",new Vector2(.5f,.5f),Vector2.zero,136,ink);
            portrait.gameObject.AddComponent<Mask>().showMaskGraphic=true;
            var art=Box(portrait,"Character portrait",new Vector2(.5f,.5f),Vector2.zero,Vector2.one*136,Color.white);
            portraitArt=art.GetComponent<Image>(); portraitArt.preserveAspect=true;
            var model=new GameObject("Model portrait",typeof(RectTransform),typeof(RawImage));
            model.transform.SetParent(portrait,false);
            var modelRect=(RectTransform)model.transform;
            modelRect.anchorMin=Vector2.zero; modelRect.anchorMax=Vector2.one;
            modelRect.offsetMin=modelRect.offsetMax=Vector2.zero;
            portraitModel=model.GetComponent<RawImage>(); portraitModel.raycastTarget=false;
            selectedClass=Label(selectionCard,"Profession","",new Vector2(0,1),new Vector2(172,-12),new Vector2(280,30),20,TextAnchor.MiddleLeft);
            selectedClass.color=gold;
            var hp=Box(selectionCard,"Health frame",new Vector2(0,1),new Vector2(172,-48),new Vector2(280,36),gold);
            var track=Box(hp,"Health track",new Vector2(0,1),new Vector2(3,-3),new Vector2(274,30),new Color(.20f,.08f,.07f));
            healthFill=Box(track,"Health fill",new Vector2(0,1),Vector2.zero,new Vector2(274,30),red).GetComponent<Image>();
            selectedHealth=Label(hp,"Health text","",new Vector2(.5f,.5f),Vector2.zero,new Vector2(270,32),24,TextAnchor.MiddleCenter);
            selectedName=Label(selectionCard,"Character name","",new Vector2(0,1),new Vector2(172,-88),new Vector2(280,34),27,TextAnchor.MiddleLeft);
            selectedStats=Label(selectionCard,"Class attributes","",new Vector2(0,1),new Vector2(172,-126),new Vector2(284,27),17,TextAnchor.MiddleLeft);
            if (ForestEnabled) {
                selectedName.rectTransform.anchoredPosition = new Vector2(172, -14);
                selectedName.rectTransform.sizeDelta = new Vector2(360, 42); selectedName.fontSize = 32; selectedName.fontStyle = FontStyle.Bold;
                selectedName.color = Color.white;
                selectedClass.rectTransform.anchoredPosition = new Vector2(172, -61); selectedClass.fontSize = 22;
                hp.anchoredPosition = new Vector2(172, -99); hp.sizeDelta = new Vector2(360, 36);
                track.sizeDelta = new Vector2(354, 30); selectedHealth.rectTransform.sizeDelta = new Vector2(350, 32);
                selectedStats.rectTransform.anchoredPosition = new Vector2(18, -174);
                selectedStats.rectTransform.sizeDelta = new Vector2(514, 54); selectedStats.fontSize = 22; selectedStats.color = Color.white;
                MakeStatusBar();
            }

            actionWheel=Box(canvas.transform,"Character action wheel",new Vector2(1,0),new Vector2(-24,32),new Vector2(350,340),Color.clear);
            Disc(actionWheel,"Wheel ornament",new Vector2(.5f,.5f),new Vector2(64,-52),244,new Color(gold.r,gold.g,gold.b,.24f));
            Disc(actionWheel,"Wheel inset",new Vector2(.5f,.5f),new Vector2(64,-52),226,new Color(ink.r,ink.g,ink.b,.85f));
            wait=RoundAction(actionWheel,"待机",new Vector2(64,-52),174,new Color(.13f,.32f,.28f),CommitWait);
            skillSlots=Box(actionWheel,"Skill slots",new Vector2(.5f,.5f),Vector2.zero,new Vector2(350,340),Color.clear);
            // Two skill nodes follow the upper-left arc of the main wait button.
            var locations=new[]{new Vector2(-82,38),new Vector2(38,126)};
            for(int i=0;i<locations.Length;i++)
            {
                int index = i;
                var slot=RoundAction(skillSlots,"技能 "+(i+1)+"\n未配置",locations[i],108,new Color(.21f,.23f,.25f),()=>{
                    if (!ForestEnabled || State.selected?.forestUnit == null) return;
                    var action = ForestSlotAction(index);
                    if (action.HasValue) SelectForestSkill(action.Value);
                });
                slot.GetComponentInChildren<Text>().fontSize=16;
                slot.interactable=false;
                AttachSkillDescription(slot);
                forestSkillButtons.Add(slot);
            }
            cancel=RoundAction(actionWheel,"取消",new Vector2(-94,-112),82,new Color(.36f,.17f,.15f),Cancel);
            if (ForestEnabled) MakeSpiritButtons();
            selectionCard.gameObject.SetActive(false); actionWheel.gameObject.SetActive(false);
        }

        void RefreshSelectionHud()
        {
            var unit=State.selected;
            bool visible=!editing && unit!=null;
            selectionCard.gameObject.SetActive(visible);
            if (!visible) HideStatusDetails();
            bool canAct=visible && !unit.enemy && !unit.acted && !battleEnded;
            bool hasFreeStanceSkill = visible && ForestEnabled && unit.forestUnit?.flight != null
                && !unit.enemy && !battleEnded && State.Forest.phase == Zilong.Forest.BattlePhase.Player;
            bool hasFreeSpiritAction = visible && ForestEnabled && !unit.enemy && !battleEnded
                && State.Forest.phase == Zilong.Forest.BattlePhase.Player && (unit.forestUnit.carriedSpiritId != null || ForestGroundSpirit() != null);
            actionWheel.gameObject.SetActive(canAct || hasFreeStanceSkill || hasFreeSpiritAction);
            skillSlots.gameObject.SetActive(canAct || hasFreeStanceSkill);
            wait.interactable=canAct && !busy;
            cancel.gameObject.SetActive(canAct && State.CanCancelMovementPreview || ForestEnabled && forestTargetAction.HasValue);
            cancel.interactable=!busy;
            if(!visible) return;
            var stats=unit.stats;
            int maxHealth=stats!=null?stats.hp:unit.hp;
            selectedName.text=unit.name;
            selectedClass.text=unit.enemy ? "" : stats!=null && !string.IsNullOrEmpty(stats.profession)?stats.profession:stats!=null?stats.name:unit.name;
            selectedHealth.text=unit.hp+" / "+maxHealth;
            healthFill.rectTransform.sizeDelta=new Vector2((ForestEnabled ? 354 : 274)*Mathf.Clamp01(unit.hp/(float)Mathf.Max(1,maxHealth)),30);
            selectedStats.text=stats!=null?$"攻 {stats.attack}   防 {stats.defense}   移 {unit.move}   射程 {stats.range}":$"移动 {unit.move}";
            portraitArt.gameObject.SetActive(stats!=null && stats.portrait!=null);
            portraitModel.gameObject.SetActive(stats==null || stats.portrait==null);
            if(stats!=null && stats.portrait!=null) portraitArt.sprite=stats.portrait;
            else if(portraitUnit!=unit) RenderModelPortrait(unit);
        }

        void RenderModelPortrait(DemoUnit unit)
        {
            portraitUnit=unit;
            if(!Application.isPlaying || !figures.ContainsKey(unit)) return;
            if(portraitTexture==null)
            {
                portraitTexture=new RenderTexture(512,512,24) { antiAliasing = 4, filterMode = FilterMode.Bilinear };
                portraitTexture.name="Selected character portrait";
                portraitModel.texture=portraitTexture;
            }
            // Render the current placeholder model until an authored portrait is assigned.
            var copy=Instantiate(figures[unit].gameObject);
            var cameraObject=new GameObject("Portrait capture camera");
            var camera=cameraObject.AddComponent<Camera>(); camera.enabled=false;
            try
            {
                copy.transform.position=new Vector3(10000,0,10000);
                foreach(var part in copy.GetComponentsInChildren<Transform>()) part.gameObject.layer=31;
                camera.cullingMask=1<<31; camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(.09f,.15f,.17f);
                camera.orthographic=true; camera.orthographicSize=.64f; camera.nearClipPlane=.1f;camera.farClipPlane=10;
                camera.transform.position=copy.transform.position+new Vector3(.7f,.9f,-2.5f);
                camera.transform.LookAt(copy.transform.position+Vector3.up*.56f);
                camera.targetTexture=portraitTexture; camera.Render();
            }
            finally
            {
                camera.targetTexture=null; copy.SetActive(false); ReleaseObject(copy); ReleaseObject(cameraObject);
            }
        }

        void ReleaseHudAssets()
        {
            if(portraitTexture!=null) {portraitTexture.Release();ReleaseObject(portraitTexture);}
            if(hudDisc!=null) ReleaseObject(hudDisc);
            if(hudDiscTexture!=null) ReleaseObject(hudDiscTexture);
        }
    }
}
