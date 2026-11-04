using System;
using UnityEngine;
using UnityEngine.UI;

namespace Mizonglin.Demo
{
    public sealed partial class BattleDemo
    {
        FighterBook bookDraft;
        readonly InputField[] fields=new InputField[7];
        int templateIndex;
        EnemyPolicy chosenPolicy;
        Text configError;
        Button policyButton;
        readonly Button[] tabs=new Button[7];
        void MakeCombatEditor()
        {
            editorPanel.sizeDelta=new Vector2(275,765);
            Action(editorPanel,"单位数值",new Vector2(0,1),new Vector2(14,-695),new Vector2(118,48),new Color(.18f,.40f,.36f),OpenBook).GetComponentInChildren<Text>().fontSize=19;
            Action(editorPanel,"地块伤害",new Vector2(0,1),new Vector2(142,-695),new Vector2(118,48),new Color(.31f,.24f,.33f),OpenHazard).GetComponentInChildren<Text>().fontSize=19;
        }
        InputField Entry(RectTransform panel,string caption,int x,int y,int width=180)
        {
            var anchor=new Vector2(0,1);
            Label(panel,caption,caption,anchor,new Vector2(x,-y),new Vector2(120,45),20,TextAnchor.MiddleLeft);
            var box=Box(panel,caption+" 输入",anchor,new Vector2(x+116,-y),new Vector2(width,45),new Color(.20f,.25f,.26f));
            var input=box.gameObject.AddComponent<InputField>(); var img=box.GetComponent<Image>(); img.raycastTarget=true; input.targetGraphic=img;
            var label=Label(box,"Text","",new Vector2(.5f,.5f),Vector2.zero,new Vector2(width-14,40),20,TextAnchor.MiddleLeft);
            label.supportRichText=false; input.textComponent=label; input.characterLimit=32; return input;
        }
        void OpenHazard()
        {
            var panel=CombatDialog("地图设置",new Vector2(590,280));
            var input=Entry(panel,"扣血百分比",55,82); input.text=draft.hazardPercent.ToString();
            var msg=Label(panel,"Help","单位在危险格结束行动时，按最大生命计算。",new Vector2(.5f,1),new Vector2(0,-142),new Vector2(554,40),18,TextAnchor.MiddleCenter);
            DialogActions(panel,"取消",CloseModal,"应用到草稿",()=>{
                if(!int.TryParse(input.text,out var value)||value<0||value>100) { msg.text="请输入 0 到 100 的整数"; return; }
                draft.hazardPercent=value; dirty=true; CloseModal(); RefreshEditor(); EditorFeedback("地块伤害已修改，请保存地图。");
            });
        }
        void OpenBook()
        {
            string error=null;
            try { bookDraft=FighterBook.Read(BookPath); }
            catch(Exception ex) { bookDraft=FighterBook.Defaults(); error="读取失败。已显示默认草稿。"+ex.Message; }
            var panel=CombatDialog("单位模板",new Vector2(900,575));
            for(int i=0;i<7;i++)
            {
                int index=i;
                tabs[i]=Action(panel,LevelMap.Templates[i],new Vector2(0,1),new Vector2(40+i*118,-76),new Vector2(106,48),ink,()=>{
                    if(ReadFields()) ShowTemplate(index);
                });
            }
            string[] captions={"名称","最大生命","攻击","防御","移动","射程","守卫半径"};
            for(int i=0;i<fields.Length;i++) fields[i]=Entry(panel,captions[i],45+i%2*413,156+i/2*62,235);
            policyButton=Action(panel,"敌方行为",new Vector2(0,1),new Vector2(458,-342),new Vector2(352,45),ink,()=>{
                chosenPolicy=(EnemyPolicy)(((int)chosenPolicy+1)%3); UpdatePolicy();
            });
            configError=Label(panel,"Message","",new Vector2(.5f,1),new Vector2(0,-401),new Vector2(810,64),19,TextAnchor.MiddleCenter);
            DialogActions(panel,"放弃并关闭",CloseModal,"保存全部模板",SaveBook);
            Action(panel,"恢复默认数值",new Vector2(.5f,0),new Vector2(0,22),new Vector2(205,55),ink,()=>{
                bookDraft=FighterBook.Defaults(); ShowTemplate(templateIndex); configError.text="七个模板已恢复默认，保存后生效。";
            });
            ShowTemplate(0); if(error!=null) configError.text=error;
        }
        void ShowTemplate(int index)
        {
            templateIndex=index; var t=bookDraft.units[index]; chosenPolicy=t.policy;
            var values=new[]{t.name,t.hp.ToString(),t.attack.ToString(),t.defense.ToString(),t.move.ToString(),t.range.ToString(),t.radius.ToString()};
            for(int i=0;i<fields.Length;i++) fields[i].text=values[i];
            for(int i=0;i<tabs.Length;i++) tabs[i].GetComponent<Image>().color=i==index?new Color(.2f,.47f,.38f):ink;
            UpdatePolicy(); configError.text="修改后保存，再开始试玩。";
        }
        void UpdatePolicy()
        {
            bool enemy=templateIndex>=4;
            policyButton.interactable=enemy;
            policyButton.GetComponentInChildren<Text>().text=enemy?"敌方行为   "+PolicyName(chosenPolicy)+"   ›":"我方 玩家控制";
            fields[6].interactable=enemy && chosenPolicy==EnemyPolicy.Guard;
        }
        bool ReadFields()
        {
            var values=new int[6];
            for(int i=0;i<6;i++)
                if(!int.TryParse(fields[i+1].text,out values[i])) { configError.text="数值栏需要填写整数。"; return false; }
            var t=new FighterTemplate(LevelMap.Templates[templateIndex],fields[0].text.Trim(),values[0],values[1],values[2],values[3],values[4],chosenPolicy){radius=values[5]};
            t.profession=bookDraft.units[templateIndex].profession;
            t.portrait=bookDraft.units[templateIndex].portrait;
            var error=t.Validate();
            if(error!=null) {configError.text=error; return false;}
            bookDraft.units[templateIndex]=t; return true;
        }
        void SaveBook()
        {
            if(!ReadFields()) return;
            try { bookDraft.Save(BookPath); CloseModal(); EditorFeedback("七个模板已保存，下次试玩生效。"); }
            catch(Exception ex) {configError.text="保存失败。"+ex.Message;}
        }
    }
}
