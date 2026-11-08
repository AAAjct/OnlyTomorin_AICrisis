using UnityEngine;

namespace TomorinTrial
{
    public enum ChampionKind : byte { FeiBi, NuoNuo, Anon, Taffy, Penguin, YaYa, Remnant }
    public enum ChampionShape : byte { Cut, Thrust, Leap, Ring, Bullets, Snipe, Annulus, Lash, Mark, Cross, Flight, Grab, Stomp, Counter, DashCut, Dive, Spiral, CrossLine, TwinPincer, BladeWave, Boomerang, Ricochet, Rocket, Mine, SweepRay, SlideVolley, Turret, Pellets, Fissure, Pillars, Vacuum, Avalanche, Tether, SilkLine, Snare, OrbitWhip }
    public enum ChampionDefense : byte { Deflect, Dodge, Approach, Retreat, Sidestep }

    // Timings are in seconds, including a separate, readable windup for EVERY beat.
    public readonly struct ChampionMove
    {
        public readonly string name, counter;
        public readonly ChampionShape shape;
        public readonly ChampionDefense defense;
        public readonly float windup, interval, recovery, range, inner, angle, damage;
        public readonly int beats;
        public bool Unblockable => defense!=ChampionDefense.Deflect;
        public float Duration => windup+interval*(beats-1)+.20f+recovery;
        public ChampionMove(string n,string c,ChampionShape s,ChampionDefense d,float w,float gap,float recover,float r,float min,float arc,float hit,int count)
        {name=n;counter=c;shape=s;defense=d;windup=w;interval=gap;recovery=recover;range=r;inner=min;angle=arc;damage=hit;beats=count;}
    }

    public static partial class TrialChampionCatalog
    {
        public const int SkillCount=16;
        public static bool IsChampion(TrialLevel level)=>level>=TrialLevel.Twins && level<=TrialLevel.Remnant;
        public static string Scene(TrialLevel l)=>l==TrialLevel.Endless?"Assets/Scenes/EndlessMist.unity":l==TrialLevel.BossRush?"Assets/Scenes/BossGauntlet.unity":"Assets/Scenes/"+(l==TrialLevel.Remnant?"PenguinRemnantSanctum":l==TrialLevel.Twins?"TwinLotusCourt":l==TrialLevel.Anon?"CrimsonSwordTerrace":l==TrialLevel.Taffy?"NeonBallisticsHarbor":l==TrialLevel.Penguin?"GlacialForge":l==TrialLevel.YaYa?"MoonSilkObservatory":"AshenCourt")+".unity";
        public static string LevelName(TrialLevel l)=>l==TrialLevel.Endless?"无尽雾潮 · 肉鸽生存":l==TrialLevel.BossRush?"百战之庭 · Boss 连战":l==TrialLevel.Remnant?"断羽终庭 · 凑企鹅残躯":l==TrialLevel.Twins?"双生莲庭 · 菲比与糯糯":l==TrialLevel.Anon?"绯刃天台 · 爱音":l==TrialLevel.Taffy?"霓虹枪港 · 塔菲":l==TrialLevel.Penguin?"冰海熔炉 · 香企鹅":l==TrialLevel.YaYa?"月丝星台 · 娅娅":l==TrialLevel.Arena?"竞技场":"庭院 (Boss · 大肥鱼)";
        public static string Name(ChampionKind k)=>k==ChampionKind.Remnant?"凑企鹅残躯 · 断羽无赦":k==ChampionKind.FeiBi?"菲比 · 逐日拳":k==ChampionKind.NuoNuo?"糯糯 · 逐月掌":k==ChampionKind.Anon?"爱音 · 绯刃剑圣":k==ChampionKind.Taffy?"塔菲 · 枪火指挥":k==ChampionKind.Penguin?"香企鹅 · 冰炉重锤":"娅娅 · 月丝执鞭者";
        public static string Lesson(TrialLevel l)=>l==TrialLevel.Remnant?"极限挑战 · 读指令反制 · 无濒死保护 · 联机每次击杀永久强化至本轮结束":l==TrialLevel.Twins?"拉开双人站位 · 金色连击可弹反 · 一人进攻时另一人会留出间隔":l==TrialLevel.Anon?"轻击会被架剑，重击与绕背破守 · 观察瞬移背斩；剑雨期间仍有近战，重击破守":l==TrialLevel.Taffy?"子弹可格挡 · 红色狙击侧闪 · 换弹时贴身压制":l==TrialLevel.Penguin?"绕背惩罚重砸 · 横移躲裂地 · 翻滚穿过扩散冰环":"外圈鞭扫向内闪 · 内圈震击向外退 · 直线鞭刺横移";
        public static float Health(ChampionKind k)=>k==ChampionKind.Remnant?7200:k==ChampionKind.FeiBi||k==ChampionKind.NuoNuo?1800:k==ChampionKind.Anon?3200:k==ChampionKind.Taffy?3000:k==ChampionKind.Penguin?3600:3300;
        public static Color Color(ChampionKind k)=>k==ChampionKind.Remnant?new Color(.78f,.16f,1):k==ChampionKind.FeiBi?new Color(1,.64f,.19f):k==ChampionKind.NuoNuo?new Color(.25f,.9f,1):k==ChampionKind.Anon?new Color(1,.32f,.52f):k==ChampionKind.Taffy?new Color(.3f,1,.72f):k==ChampionKind.Penguin?new Color(.4f,.7f,1):new Color(.78f,.44f,1);
        static ChampionMove M(string n,string c,ChampionShape s,ChampionDefense d,float w,float gap,float rec,float r,float min,float arc,float hit,int beats=1)=>new ChampionMove(n,c,s,d,w,gap,rec,r,min,arc,hit,beats);
        static readonly ChampionMove[][] moves={
            new[]{
                M("逐日三叠拳","连续弹反 / 向侧面闪避",ChampionShape.Cut,ChampionDefense.Deflect,.72f,.56f,.85f,3.5f,0,88,34,3),
                M("破门崩拳","侧闪，拳路锁定后不追踪",ChampionShape.Thrust,ChampionDefense.Sidestep,.95f,.65f,1.0f,7,0,20,48),
                M("莲步回旋踢","弹反踢击 / 退到扇区外",ChampionShape.Cut,ChampionDefense.Deflect,.85f,.62f,.9f,4.3f,0,145,38,2),
                M("踏日跃击","离开地面落点圆圈",ChampionShape.Leap,ChampionDefense.Dodge,1.15f,.7f,1.25f,3.5f,0,180,52),
                M("双生交错·阳","观察各自拳路，侧向穿出",ChampionShape.Cross,ChampionDefense.Deflect,.95f,.7f,1.1f,6.5f,0,45,39,2),
                M("寸劲五连","按金色脉冲连续弹反",ChampionShape.Cut,ChampionDefense.Deflect,.65f,.46f,1.1f,3.6f,0,85,32,5),
                M("虚步延迟肘","等蓄力结束再弹反",ChampionShape.Thrust,ChampionDefense.Deflect,1.38f,.6f,1.2f,4.4f,0,55,48),
                M("孤阳·流星拳","横向闪避三次锁定拳路",ChampionShape.Thrust,ChampionDefense.Sidestep,.95f,.8f,1.5f,7,0,24,43,3)},
            new[]{
                M("逐月双掌","两次弹反 / 后退",ChampionShape.Cut,ChampionDefense.Deflect,.86f,.66f,.9f,4.0f,0,110,37,2),
                M("月影穿心","向两侧闪开",ChampionShape.Thrust,ChampionDefense.Sidestep,1.08f,.65f,1.1f,6,0,24,46),
                M("折桂旋踢","弹反 / 拉开四米",ChampionShape.Cut,ChampionDefense.Deflect,.95f,.7f,1.05f,4.8f,0,155,39,2),
                M("抱月坠地","离开落点后反击收招",ChampionShape.Leap,ChampionDefense.Dodge,1.28f,.7f,1.4f,3.8f,0,180,50),
                M("双生交错·阴","侧向躲开交错掌路",ChampionShape.Cross,ChampionDefense.Deflect,1.1f,.75f,1.1f,6.5f,0,45,39,2),
                M("冷月四连","按掌击节奏弹反",ChampionShape.Cut,ChampionDefense.Deflect,.8f,.55f,1.15f,4,0,100,34,4),
                M("回身摘星","等待回身金光再弹反",ChampionShape.Cut,ChampionDefense.Deflect,1.4f,.6f,1.2f,4.3f,0,160,45),
                M("孤月·天降掌","引出三个落点后持续移动",ChampionShape.Leap,ChampionDefense.Dodge,1.2f,.95f,1.6f,3.3f,0,180,44,3)},
            new[]{
                M("绯刃·三拍","连续点按格挡，积累敌方架势",ChampionShape.Cut,ChampionDefense.Deflect,.72f,.48f,.75f,3.9f,0,90,35,3),
                M("危·穿云突刺","蓝色直线锁定后侧闪",ChampionShape.Thrust,ChampionDefense.Sidestep,.96f,.6f,1.05f,7.5f,0,18,51),
                M("拔刀·居合返","等待刀鞘闪光，两次弹反",ChampionShape.Cut,ChampionDefense.Deflect,1.35f,.55f,1.1f,5.2f,0,115,46,2),
                M("危·落樱下段","后撤到五米外 / 向后闪避",ChampionShape.Cut,ChampionDefense.Retreat,1.08f,.7f,1.1f,4.8f,0,165,48),
                M("雷切五重奏","五次拼刀，最后一刀有延迟",ChampionShape.Cut,ChampionDefense.Deflect,.78f,.54f,1.2f,4.3f,0,115,37,5),
                M("飞燕断空","闪出落点，追击落地硬直",ChampionShape.Leap,ChampionDefense.Dodge,1.16f,.8f,1.25f,3.7f,0,180,52,2),
                M("镜返·十字斩","两次弹反 / 绕出前方扇区",ChampionShape.Cross,ChampionDefense.Deflect,.88f,.6f,1.2f,4.8f,0,100,43,2),
                M("终式·八相绯雨","八拍拼刀；也可退到刀圈外",ChampionShape.Cut,ChampionDefense.Deflect,.9f,.48f,1.8f,4.6f,0,125,36,8)},
            new[]{
                M("点射·三连音","按弹道节奏格挡或横移",ChampionShape.Bullets,ChampionDefense.Deflect,.85f,.62f,1.05f,26,0,8,30,3),
                M("危·红线狙杀","红线停止追踪后横向闪避",ChampionShape.Snipe,ChampionDefense.Sidestep,1.5f,.7f,1.4f,35,0,5,58),
                M("扇面压制","弹幕有空隙，可格挡单发",ChampionShape.Bullets,ChampionDefense.Deflect,1.0f,.66f,1.15f,26,0,52,27,3),
                M("枪托破围","近身两次格挡 / 后退",ChampionShape.Cut,ChampionDefense.Deflect,.7f,.6f,.85f,3.2f,0,100,36,2),
                M("跃迁扫射","追踪停止后移动；落地会换弹",ChampionShape.Flight,ChampionDefense.Deflect,1.15f,.7f,1.65f,28,0,38,30,4),
                M("交叉火网","观察交错弹道，利用掩体",ChampionShape.Bullets,ChampionDefense.Deflect,.95f,.7f,1.2f,30,0,76,28,4),
                M("危·定点榴弹","离开标记圆圈；爆炸不穿掩体",ChampionShape.Mark,ChampionDefense.Dodge,1.35f,.85f,1.5f,3.2f,0,180,50,3),
                M("终式·弹雨协奏","格挡 / 找扇形空隙；结束长换弹",ChampionShape.Bullets,ChampionDefense.Deflect,1.1f,.58f,2.3f,30,0,96,27,7)},
            new[]{
                M("锻炉·双重锤","弹反锤头，或绕到背后",ChampionShape.Cut,ChampionDefense.Deflect,1.1f,.9f,1.3f,5.2f,0,105,48,2),
                M("危·裂冰直落","横移离开地面长条裂痕",ChampionShape.Thrust,ChampionDefense.Sidestep,1.42f,.8f,1.5f,15,0,16,60),
                M("危·冰海震环","看冰环接近时翻滚穿过",ChampionShape.Ring,ChampionDefense.Dodge,1.25f,1.05f,1.55f,18,0,180,46,3),
                M("回炉横扫","弹反 / 向后退六米",ChampionShape.Cut,ChampionDefense.Deflect,1.2f,.8f,1.2f,5.9f,0,155,47),
                M("危·陨锤天降","引出落点，绕背惩罚落地",ChampionShape.Leap,ChampionDefense.Dodge,1.6f,1.1f,1.85f,4.5f,0,180,64),
                M("危·空心熔爆","内圈安全，向锤主靠近",ChampionShape.Annulus,ChampionDefense.Approach,1.55f,.85f,1.6f,11,4,180,57),
                M("碎星四连锻","四次慢快交替重锤，可弹反",ChampionShape.Cut,ChampionDefense.Deflect,1.15f,.74f,1.6f,5.4f,0,115,44,4),
                M("终式·冰炉纪元","持续走出标记区，保留翻滚气力",ChampionShape.Mark,ChampionDefense.Dodge,1.55f,1.05f,2.0f,4.1f,0,180,55,5)},
            new[]{
                M("月丝·双重鞭刺","横向移动 / 迎鞭弹反",ChampionShape.Lash,ChampionDefense.Deflect,.95f,.7f,1.0f,10,0,14,38,2),
                M("危·满月外环","向内闪入三米安全区",ChampionShape.Annulus,ChampionDefense.Approach,1.2f,.8f,1.25f,10,3.2f,180,48),
                M("危·蚀心近环","离开四米内圈",ChampionShape.Mark,ChampionDefense.Retreat,1.12f,.7f,1.1f,4.2f,0,180,47),
                M("折月回鞭","鞭尖两拍，横移或弹反",ChampionShape.Lash,ChampionDefense.Deflect,1.0f,.62f,1.15f,12,0,18,40,2),
                M("危·织夜缚阵","三个独立落点，持续侧移",ChampionShape.Mark,ChampionDefense.Dodge,1.2f,.85f,1.4f,3.0f,0,180,45,3),
                M("凌空·银蛇舞","离开落点，落地后贴身",ChampionShape.Leap,ChampionDefense.Dodge,1.4f,.95f,1.5f,3.8f,0,180,52,2),
                M("危·双月同蚀","外环向内闪，两拍后再准备",ChampionShape.Annulus,ChampionDefense.Approach,1.3f,.95f,1.5f,12,3.4f,180,46,2),
                M("终式·七弦断月","七道鞭路锁定后横移；可弹反",ChampionShape.Lash,ChampionDefense.Deflect,1.1f,.58f,1.8f,13,0,20,37,7)}
        };
        static readonly ChampionMove[][] extensions={
            new[]{
                M("擒龙·贴身锁肩","红色抓取：格挡会消耗大量耐力，也可侧闪或后撤",ChampionShape.Grab,ChampionDefense.Dodge,.85f,.6f,1.2f,2.8f,0,50,52),
                M("震步·碎莲踏","看到脚下蓄力，退出四米内圈",ChampionShape.Stomp,ChampionDefense.Retreat,1.05f,.65f,1.1f,4.3f,0,180,44),
                M("截拳·守势反击","轻击会触发反击；重击破守或等其落空",ChampionShape.Counter,ChampionDefense.Deflect,1.0f,.6f,1.0f,3.6f,0,100,42),
                M("追风·折线崩拳","锁定拳路后侧闪，落空可追击",ChampionShape.DashCut,ChampionDefense.Deflect,.7f,.56f,.85f,4.0f,0,65,39,3),
                M("腾龙·飞膝坠击","飞跃后锁定落点；离开圆圈",ChampionShape.Dive,ChampionDefense.Dodge,1.2f,.85f,1.35f,3.5f,0,180,54),
                M("旋风·游身连踢","围绕身侧旋踢，退到六米外或逐拍弹反",ChampionShape.Spiral,ChampionDefense.Deflect,.8f,.55f,1.0f,5.4f,0,70,37,4),
                M("裂空·十字双肘","沿斜角离开两道交叉近身拳路",ChampionShape.CrossLine,ChampionDefense.Sidestep,.95f,.65f,1.1f,5.2f,0,20,48),
                M("双生·换位围猎","两人飞跃换位；面向当前出拳者防守",ChampionShape.TwinPincer,ChampionDefense.Deflect,1.05f,.72f,1.3f,4.0f,0,100,42,3)},
            new[]{
                M("缠月·搭腕牵引","横移避开三米擒腕；可弹反断掌",ChampionShape.Tether,ChampionDefense.Deflect,.9f,.6f,1.0f,3.8f,0,25,40),
                M("镇月·回身震足","离开局部震足圆圈",ChampionShape.Stomp,ChampionDefense.Retreat,1.1f,.7f,1.1f,4.7f,0,180,43),
                M("借月·卸力还掌","用重击破守，或骗出还掌后反击",ChampionShape.Counter,ChampionDefense.Deflect,1.05f,.6f,1.05f,4,0,100,43),
                M("流云·穿花掌","连续侧向切入，可弹反每次掌击",ChampionShape.DashCut,ChampionDefense.Deflect,.8f,.62f,1.0f,4.4f,0,75,38,3),
                M("摘星·翻身落掌","空翻跨越后坠掌，离开落点",ChampionShape.Dive,ChampionDefense.Dodge,1.25f,.85f,1.4f,3.8f,0,180,53),
                M("回雪·八卦游掌","四次环身掌，随身转向或退出掌圈",ChampionShape.Spiral,ChampionDefense.Deflect,.9f,.6f,1.15f,5.8f,0,65,36,4),
                M("月痕·纵横截步","向斜角走出近身十字截击区",ChampionShape.CrossLine,ChampionDefense.Sidestep,1.0f,.65f,1.15f,5.5f,0,20,47),
                M("双生·阴阳倒转","观察换位落点，再处理正面掌击",ChampionShape.TwinPincer,ChampionDefense.Deflect,1.15f,.76f,1.35f,4.2f,0,105,41,3)},
            new[]{
                M("镜心·反架势","轻击触发还刀；重击破守或骗招",ChampionShape.Counter,ChampionDefense.Deflect,.95f,.5f,1.1f,4.2f,0,95,44),
                M("绯月·离刃剑气","横移避开移动剑气，也能迎面弹反",ChampionShape.BladeWave,ChampionDefense.Deflect,.8f,.52f,1.0f,25,0,14,40,3),
                M("惊鸿·逐影闪切","连续三次切入，按金光拼刀",ChampionShape.DashCut,ChampionDefense.Deflect,.68f,.50f,.95f,4.2f,0,85,40,3),
                M("纵横·十字剑狱","沿斜角离开十字剑气路径",ChampionShape.CrossLine,ChampionDefense.Sidestep,1.0f,.6f,1.2f,11,0,15,51),
                M("燕返·翻空落刃","跃过身侧后落剑，闪出落点",ChampionShape.Dive,ChampionDefense.Dodge,1.1f,.8f,1.25f,3.5f,0,180,52),
                M("游龙·环身剑舞","移动环身五斩；转向拼刀或拉开距离",ChampionShape.Spiral,ChampionDefense.Deflect,.78f,.5f,1.25f,5.2f,0,75,36,5),
                M("破势·剑柄锁喉","红色近身抓取，后撤或侧闪",ChampionShape.Grab,ChampionDefense.Dodge,.8f,.5f,1.1f,2.9f,0,48,50),
                M("归鞘·回旋飞刃","飞剑有去回两条可见轨迹；格挡或闪避",ChampionShape.Boomerang,ChampionDefense.Deflect,1.0f,.7f,1.5f,18,0,18,47)},
            new[]{
                M("跳弹·折射猎杀","子弹遇墙反弹一次，格挡可终止弹道",ChampionShape.Ricochet,ChampionDefense.Deflect,.9f,.62f,1.15f,30,0,65,29,4),
                M("危·破甲火箭","侧闪火箭；重斩可击碎飞行弹体",ChampionShape.Rocket,ChampionDefense.Sidestep,1.2f,.85f,1.4f,30,0,8,58,2),
                M("雷区·拒止布设","地雷触发后再延时爆炸；可用剑清除",ChampionShape.Mine,ChampionDefense.Dodge,1.1f,.72f,1.4f,2.7f,0,180,48,4),
                M("危·横扫火线","预告后定速横扫，穿越火线或绕近身",ChampionShape.SweepRay,ChampionDefense.Dodge,1.25f,.8f,1.45f,28,3.5f,110,52),
                M("滑铲·贴地速射","横向滑移同时射击；面向枪口格挡",ChampionShape.SlideVolley,ChampionDefense.Deflect,.75f,.52f,1.2f,27,0,32,29,5),
                M("哨戒·三角交火","炮台先锁定再射击，靠近可砍掉炮台",ChampionShape.Turret,ChampionDefense.Deflect,1.1f,.85f,1.6f,24,0,35,28,3),
                M("霰火·破围轰击","扇形霰弹近强远弱；格挡或侧闪",ChampionShape.Pellets,ChampionDefense.Deflect,.8f,.7f,1.4f,20,0,100,33,2),
                M("空袭·迁跃投雷","飞跃换位，按三个落雷圆圈移动",ChampionShape.Dive,ChampionDefense.Dodge,1.3f,.95f,1.8f,3.2f,0,180,48,3)},
            new[]{
                M("回炉·回旋飞锤","锤体飞出再返回，观察轨迹格挡或侧闪",ChampionShape.Boomerang,ChampionDefense.Deflect,1.35f,.8f,1.7f,19,0,18,54),
                M("裂地·冰脊追震","裂痕沿固定直线逐段推进，横移躲开",ChampionShape.Fissure,ChampionDefense.Sidestep,1.3f,.65f,1.5f,20,0,18,53,5),
                M("冰柱·八方封途","预告圆圈后冰柱涌出，可重击打碎",ChampionShape.Pillars,ChampionDefense.Dodge,1.4f,.75f,1.6f,2.5f,0,180,48,4),
                M("天砧·四象断界","十字锤浪，沿对角线进入安全象限",ChampionShape.CrossLine,ChampionDefense.Sidestep,1.4f,.75f,1.6f,18,0,20,58),
                M("熔心·引力重锻","蓄力缓慢牵引；向外跑出爆炸圈",ChampionShape.Vacuum,ChampionDefense.Retreat,1.8f,.8f,1.85f,7.2f,0,180,64),
                M("暴风·旋锤游猎","旋锤沿圆弧追击，退出六米锤圈",ChampionShape.Spiral,ChampionDefense.Deflect,1.15f,.72f,1.7f,6.3f,0,80,46,4),
                M("碎冰·近身跺震","离开局部四米重震区",ChampionShape.Stomp,ChampionDefense.Retreat,1.15f,.75f,1.25f,4.8f,0,180,52),
                M("雪崩·滚石洪流","慢速滚石留出间隔；向侧面走位",ChampionShape.Avalanche,ChampionDefense.Dodge,1.5f,.85f,1.9f,28,0,65,55,5)},
            new[]{
                M("回月·折返银鞭","去回鞭路可见，横移或弹反鞭尖",ChampionShape.Boomerang,ChampionDefense.Deflect,1.0f,.7f,1.4f,16,0,15,44),
                M("牵星·缠腕回拉","鞭线锁定后侧移；格挡可断牵引",ChampionShape.Tether,ChampionDefense.Deflect,.95f,.65f,1.2f,13,0,14,42),
                M("织月·丝线封路","丝线亮起后生效；翻滚穿越或斩断",ChampionShape.SilkLine,ChampionDefense.Dodge,1.15f,.8f,1.4f,12,0,25,43,3),
                M("乱舞·螺旋追鞭","鞭圈随身旋转，贴内圈或向外撤离",ChampionShape.Spiral,ChampionDefense.Deflect,.9f,.58f,1.3f,7.2f,0,55,38,5),
                M("藏锋·借刃反鞭","轻击触发回鞭；用重击破守或骗招",ChampionShape.Counter,ChampionDefense.Deflect,1.0f,.6f,1.1f,5,0,100,44),
                M("飞天·燕舞落鞭","飞跃横穿战场，离开落鞭圆圈",ChampionShape.Dive,ChampionDefense.Dodge,1.25f,.85f,1.45f,3.7f,0,180,51,2),
                M("结月·迟发缚阵","先引出缚阵，触发后移出；可斩断阵心",ChampionShape.Snare,ChampionDefense.Dodge,1.1f,.8f,1.45f,2.9f,0,180,46,4),
                M("月轮·旋臂天幕","旋转长鞭内侧安全，闪入三米内圈",ChampionShape.OrbitWhip,ChampionDefense.Approach,1.25f,.85f,1.6f,12,3.2f,360,49)}
        };
        public static ChampionMove Move(ChampionKind k,int index)
        {
            if(k==ChampionKind.Anon)return anonSignatureMoves[Mathf.Clamp(index,0,SkillCount-1)];
            if(k==ChampionKind.FeiBi || k==ChampionKind.NuoNuo)return TwinSignatureMove(k,Mathf.Clamp(index,0,SkillCount-1));
            if(k==ChampionKind.Remnant)return remnantMoves[Mathf.Clamp(index,0,SkillCount-1)];
            index=Mathf.Clamp(index,0,SkillCount-1);var m=index<8?moves[(int)k][index]:extensions[(int)k][index-8];
            if(TrySignatureMove(k,index,out var signature))return signature;
            // Preserve slot IDs for saves and replication, but retire repeated aerial slams.
            if(m.shape==ChampionShape.Leap || m.shape==ChampionShape.Dive || m.shape==ChampionShape.TwinPincer)
            {
                if(k==ChampionKind.Anon)m=M("踏影·进步挑斩","看清前摇后弹反或侧闪",ChampionShape.Thrust,ChampionDefense.Deflect,.95f,.62f,1.3f,4.8f,0,42,48);
                else if(k==ChampionKind.Taffy)m=M("迁步·侧滑点射","沿枪线侧移，贴身反击换弹",ChampionShape.SlideVolley,ChampionDefense.Sidestep,1.2f,.7f,1.5f,24,0,30,38,3);
                else if(k==ChampionKind.Penguin)m=M("沉炉·横锤破阵","向后撤出锤弧或弹反",ChampionShape.Cross,ChampionDefense.Deflect,1.35f,.8f,1.6f,4.8f,0,140,58);
                else if(k==ChampionKind.YaYa)m=M("回燕·双折鞭","逐段弹反或退出鞭尖",ChampionShape.Lash,ChampionDefense.Deflect,1.1f,.75f,1.3f,7,0,100,43,2);
                else m=M("进步·破阵掌","侧闪掌路或弹反",ChampionShape.Thrust,ChampionDefense.Deflect,1.05f,.7f,1.3f,4.2f,0,50,45,2);
            }
            bool quick=m.shape==ChampionShape.Cut || m.shape==ChampionShape.Cross || m.shape==ChampionShape.Lash;
            bool hammer=k==ChampionKind.Penguin;
            float wind=quick?Mathf.Clamp(m.windup*(hammer?.43f:.48f),hammer?.40f:.30f,hammer?.62f:.66f):m.shape==ChampionShape.Thrust?m.windup*(hammer?.48f:.60f):m.windup*(hammer?.56f:.67f);
            float interval=Mathf.Max(hammer?.36f:.28f,m.interval*(hammer?.57f:k==ChampionKind.Taffy?.72f:.62f));
            float recovery=m.recovery*(hammer?.48f:k==ChampionKind.Taffy?1:.64f);
            return new ChampionMove(m.name,m.counter,m.shape,m.defense,wind,interval,Mathf.Max(.45f,recovery),m.range,m.inner,m.angle,m.damage*(hammer?1.10f:1),m.beats);
        }
    }
}
