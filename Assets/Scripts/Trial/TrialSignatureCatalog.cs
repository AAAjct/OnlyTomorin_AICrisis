namespace TomorinTrial
{
    public static partial class TrialChampionCatalog
    {
        static bool TrySignatureMove(ChampionKind k,int i,out ChampionMove move)
        {
            move=default;
            if(k==ChampionKind.Taffy && i==4)move=M("跃迁·双哨交火","两座哨戒先锁后射，可斩毁",ChampionShape.Turret,ChampionDefense.Deflect,.9f,.6f,.9f,24,0,20,29);
            else if(k==ChampionKind.Taffy && i==15)move=M("空袭·五点布雷","五枚地雷延迟触发，可用刀拆除",ChampionShape.Mine,ChampionDefense.Dodge,1.05f,.6f,1.2f,2.2f,0,180,42);
            else if(k==ChampionKind.Penguin && i==4)move=M("天砧·冰脊破城","粗冰脊锁向后推进，横向离开",ChampionShape.Fissure,ChampionDefense.Sidestep,1.05f,.6f,.95f,9,0,15,61);
            else if(k==ChampionKind.Penguin && i==7)move=M("纪元·三岔冻土","三条分叉冰脊，站入分叉间隙",ChampionShape.Fissure,ChampionDefense.Sidestep,1.15f,.7f,1.2f,16,0,46,52,2);
            else if(k==ChampionKind.Penguin && i==11)move=M("断界·四象冰棱","四条轴线先亮后起，进入对角象限",ChampionShape.CrossLine,ChampionDefense.Sidestep,1.1f,.6f,1,16,0,18,57);
            else if(k==ChampionKind.YaYa && i==2)move=M("蚀心·收丝绞","双结从外向内收丝，退出交点",ChampionShape.Snare,ChampionDefense.Dodge,.85f,.6f,.8f,4,0,180,45);
            else if(k==ChampionKind.YaYa && i==4)move=M("织夜·三结牵月","三丝逐渐收拢，穿过尚未闭合的间隙",ChampionShape.SilkLine,ChampionDefense.Dodge,.95f,.6f,.9f,9,0,25,44);
            else if(k==ChampionKind.YaYa && i==5)move=M("银蛇·对角缝合","两条丝路缝向交点，横向离开",ChampionShape.SilkLine,ChampionDefense.Dodge,1,.6f,.9f,8,0,25,47,2);
            else if(k==ChampionKind.YaYa && i==13)move=M("星梭·四角归丝","四角丝梭向中心合拢，躲开丝线",ChampionShape.SilkLine,ChampionDefense.Dodge,1.15f,.65f,1.1f,10,0,25,49);
            else return false;
            return true;
        }
        // These timings are already authored in real seconds. Do not apply the legacy speed multiplier.
        static readonly ChampionMove[] anonSignatureMoves={
            M("绯弦·错拍双斩","双刀左右交替，逐拍拼刀",ChampionShape.Cut,ChampionDefense.Deflect,.56f,.38f,.65f,4.2f,0,85,36,3),
            M("贯星·踏刃突进","锁定后的直刺不再追踪，横移",ChampionShape.Thrust,ChampionDefense.Sidestep,.70f,.5f,.7f,10,0,15,49),
            M("镜步·背月反斩","残影抵达背后后才挥刀，转身弹反",ChampionShape.Cross,ChampionDefense.Deflect,.62f,.40f,.7f,4.5f,0,105,43,2),
            M("樱切·贴地半月","低位长弧，后撤出刀尖",ChampionShape.Cut,ChampionDefense.Retreat,.82f,.5f,.8f,5.4f,0,150,46),
            M("天契·五芒剑庭","五芒线与落剑光柱均会生效；爱音继续近战",ChampionShape.Mark,ChampionDefense.Dodge,.90f,.8f,.45f,5.6f,0,180,44),
            M("双翼·扇刃齐鸣","双刀扇面飞刃，寻找刃间空隙",ChampionShape.BladeWave,ChampionDefense.Deflect,.68f,.55f,.75f,24,0,100,31,2),
            M("剪影·交锋花结","双刀交叉留下延迟剪切剑痕",ChampionShape.Cross,ChampionDefense.Deflect,.65f,.48f,.8f,5,0,105,40,2),
            M("绯雨·逐星七拍","七次不同方向斩击，退开或逐拍弹反",ChampionShape.Cut,ChampionDefense.Deflect,.62f,.34f,1.1f,4.7f,0,115,33,7),
            M("镜心·震剑拒客","重击或绕背破守，轻击会被震退",ChampionShape.Counter,ChampionDefense.Deflect,.90f,.5f,.8f,4.6f,0,100,43),
            M("离弦·飞刃接续","先射锚刃，再移至刃端连斩",ChampionShape.BladeWave,ChampionDefense.Deflect,.65f,.62f,.8f,25,0,18,39,3),
            M("惊鸿·三折追星","每次切入各有前摇，逐拍防守",ChampionShape.DashCut,ChampionDefense.Deflect,.58f,.43f,.8f,4.3f,0,90,37,3),
            M("星牢·五角合围","星边先亮后切，离开边线",ChampionShape.Mark,ChampionDefense.Dodge,.90f,.7f,.9f,6.8f,0,180,49),
            M("倒悬·落剑追身","两轮点名剑雨，保持移动",ChampionShape.Mark,ChampionDefense.Dodge,.85f,.9f,.8f,2.2f,0,180,43,2),
            M("花轮·双刃游龙","双刀旋转同时发出切线飞刃",ChampionShape.Spiral,ChampionDefense.Deflect,.65f,.42f,.85f,5.2f,0,75,34,4),
            M("裁云·一线天","三把巨剑沿固定直线依次落下",ChampionShape.Thrust,ChampionDefense.Sidestep,.95f,.5f,.9f,14,0,12,53),
            M("归星·逆轨飞剑","双剑沿弧线离手再回收",ChampionShape.Boomerang,ChampionDefense.Deflect,.80f,.6f,1.0f,20,0,18,42)
        };
        static readonly string[] sunNames={"朝日·三重拳芒","日冕·贯阵","焰足·弯弓踢","曜日·升龙轨","双生·日轨交错","寸阳·五脉爆","炽心·迟发肘","流星·折光拳","擒龙·烈阳锁","碎莲·六瓣火印","截日·蓄拳还击","追风·折射火拳","腾龙·烙日膝","日轮·切线飞踢","裂空·双阳交轨","终阳·放射拳雨"};
        static readonly string[] moonNames={"新月·镜掌回声","缺月·双影夹心","折桂·弦月残照","抱月·迟影三叠","双生·月下错位","冷月·四象留痕","摘星·逆序回放","孤月·步步生莲","缠月·搭腕月钩","镇月·空心残像","借月·镜心还掌","流云·穿花留月","摘星·两仪轮换","回雪·八卦月盘","月痕·棋盘截步","终月·镜湖碎影"};
        static ChampionMove TwinSignatureMove(ChampionKind kind,int i)
        {
            bool sun=kind==ChampionKind.FeiBi;
            var m=i<8?moves[(int)kind][i]:extensions[(int)kind][i-8];
            var shape=m.shape;
            // Neither twin uses the old shared jump-and-circle attack.
            if(shape==ChampionShape.Leap || shape==ChampionShape.Dive || shape==ChampionShape.TwinPincer)shape=sun?ChampionShape.DashCut:ChampionShape.Lash;
            return new ChampionMove(sun?sunNames[i]:moonNames[i],sun?"拳芒沿固定轨迹推进；近战可弹反，注意火拳间隙":"月影落点锁定后延迟引爆；走出月痕再反击",shape,m.defense,
                sun?.50f+i%3*.11f:.68f+i%3*.12f,sun?.37f+i%2*.09f:.58f+i%2*.12f,.65f,m.range,m.inner,m.angle,m.damage,m.beats);
        }
    }
}
