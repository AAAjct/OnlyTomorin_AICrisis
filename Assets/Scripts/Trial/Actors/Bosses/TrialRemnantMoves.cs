namespace TomorinTrial
{
    public static partial class TrialChampionCatalog
    {
        static readonly ChampionMove[] remnantMoves={
            M("残羽·六拍断命","六拍快斩，逐拍弹反",ChampionShape.Cut,ChampionDefense.Deflect,.30f,.29f,.65f,4.5f,0,105,61,6),
            M("夺息·穿喉","直线突刺；横向避开",ChampionShape.Thrust,ChampionDefense.Sidestep,.42f,.4f,.65f,11,0,13,88),
            M("虚鞘·迟斩","延迟拔刀，勿提前翻滚",ChampionShape.Cut,ChampionDefense.Deflect,.98f,.36f,.70f,5.8f,0,125,82,2),
            M("断腿·低羽扫","后撤出近身扇区",ChampionShape.Cut,ChampionDefense.Retreat,.48f,.4f,.60f,5,0,160,77),
            M("审判·三重剑痕","斜向离开十字路径",ChampionShape.CrossLine,ChampionDefense.Sidestep,.60f,.45f,.70f,13,0,12,83,3),
            M("无赦·折线追斩","三次切入，弹反或侧闪",ChampionShape.DashCut,ChampionDefense.Deflect,.38f,.34f,.70f,4.6f,0,80,65,3),
            M("反命·镜心","轻击触发反击，重击破守",ChampionShape.Counter,ChampionDefense.Deflect,.68f,.4f,.65f,4.8f,0,105,85),
            M("葬歌·九刃","九拍剑阵，留气力逐拍应对",ChampionShape.Cut,ChampionDefense.Deflect,.44f,.28f,.95f,5.2f,0,120,57,9),
            M("离羽·追魂剑气","横移或迎面弹反剑气",ChampionShape.BladeWave,ChampionDefense.Deflect,.44f,.31f,.75f,28,0,12,68,4),
            M("归墟·回旋残刃","飞剑去回两段，留意回程",ChampionShape.Boomerang,ChampionDefense.Deflect,.55f,.5f,.8f,21,0,18,83),
            M("迟刑·点名剑墓","点名后持续离开落点",ChampionShape.Mark,ChampionDefense.Dodge,.82f,.58f,.85f,2.8f,0,180,84,3),
            M("噬命·锁颈","近身抓取不可格挡",ChampionShape.Grab,ChampionDefense.Dodge,.44f,.4f,.7f,3.2f,0,45,105),
            M("残月·空心剑域","靠近进入内圈",ChampionShape.Annulus,ChampionDefense.Approach,.72f,.62f,.75f,12,3.2f,180,87,2),
            M("碎躯·踏魂","退出脚下近身震击",ChampionShape.Stomp,ChampionDefense.Retreat,.58f,.4f,.65f,4.6f,0,180,93),
            M("逆羽·游身五斩","追踪环身剑舞，弹反或退开",ChampionShape.Spiral,ChampionDefense.Deflect,.46f,.32f,.75f,5.4f,0,75,66,5),
            M("终庭·三重寂灭","翻滚穿过扩散剑环",ChampionShape.Ring,ChampionDefense.Dodge,.78f,.64f,1.0f,19,0,180,79,3)
        };
    }
}
