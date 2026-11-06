using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialWorld
    {
        void BuildChampionArena(TrialLevel level)
        {
            Root.name=TrialChampionCatalog.LevelName(level);
            RenderSettings.fogDensity=.008f;
            var accent=level==TrialLevel.Twins?Gold:level==TrialLevel.Anon?Crimson:level==TrialLevel.Taffy?Steel:level==TrialLevel.Penguin?Steel:Water;
            Box("48m arena foundation",new Vector3(0,-.65f,25),new Vector3(50,1.2f,50),DarkStone);
            Box("Continuous walkable arena",new Vector3(0,-.16f,25),new Vector3(48,.28f,48),Stone);
            for(int x=-11;x<=11;x++)for(int z=1;z<=24;z++)
            {
                var tile=Box("Arena paving",new Vector3(x*2,-.025f,z*2),new Vector3(1.95f,.05f,1.95f),(x+z)%7==0?accent:Stone,false);
                tile.localEulerAngles=new Vector3(0,(x+z)%2*90,0);
            }
            // These are actual collision rails, with visible geometry matching their footprints.
            for(int side=-1;side<=1;side+=2)
            {
                Box("Arena edge rail",new Vector3(side*24,1,25),new Vector3(.5f,2,49),DarkStone);
                Box("Rail inlay",new Vector3(side*24,2.04f,25),new Vector3(.6f,.08f,49),accent,false);
                for(int z=3;z<=47;z+=4)Box("Boundary post",new Vector3(side*24,1.35f,z),new Vector3(.85f,2.7f,.85f),accent,false);
            }
            Box("North boundary",new Vector3(0,1,49),new Vector3(48,2,.5f),DarkStone);
            Box("South boundary",new Vector3(0,1,1),new Vector3(48,2,.5f),DarkStone);
            Gate=new GameObject("Boss challenge unlocked");Gate.transform.SetParent(Root,false);Gate.SetActive(false);
            ArenaEntrance=new GameObject("Challenge active");ArenaEntrance.transform.SetParent(Root,false);ArenaEntrance.SetActive(false);
            Ring(new Vector3(0,.035f,25),16,.055f,accent);Ring(new Vector3(0,.04f,25),15.7f,.025f,Gold);
            if(level==TrialLevel.Twins || level==TrialLevel.Endless || level==TrialLevel.BossRush)LotusCourt();
            else if(level==TrialLevel.Anon)SwordTerrace();
            else if(level==TrialLevel.Taffy)BallisticsHarbor();
            else if(level==TrialLevel.Penguin)GlacialForge();
            else if(level==TrialLevel.Remnant)RemnantSanctum();
            else MoonObservatory();
            RenderSettings.skybox.SetColor("_HorizonColor",RenderSettings.fogColor*1.6f);
            RenderSettings.skybox.SetColor("_ZenithColor",level==TrialLevel.Twins?new Color(.18f,.28f,.36f):level==TrialLevel.Anon?new Color(.28f,.12f,.19f):level==TrialLevel.Penguin?new Color(.19f,.35f,.47f):level==TrialLevel.YaYa?new Color(.055f,.035f,.13f):new Color(.02f,.08f,.12f));
            RenderSettings.ambientEquatorColor=RenderSettings.ambientSkyColor*.68f;RenderSettings.ambientGroundColor=RenderSettings.ambientSkyColor*.35f;
            Shrine();
            BatchStaticGeometry();
        }
        void RemnantSanctum()
        {
            Stone.SetColor("_BaseColor",new Color(.12f,.095f,.17f));
            RenderSettings.fogColor=new Color(.065f,.035f,.10f);RenderSettings.ambientSkyColor=new Color(.35f,.25f,.43f);
            Ring(new Vector3(0,.065f,25),10,.08f,RedGlow);
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6;var position=new Vector3(Mathf.Sin(a)*29,3,25+Mathf.Cos(a)*29);
                var pillar=Box("Shattered memorial",position,new Vector3(1.2f,6+i%3,.9f),DarkStone,false);pillar.localEulerAngles=new Vector3(i%3*7,i*30,i%2==0?12:-9);
                Box("Violet scar",position+Vector3.up*1.2f,new Vector3(.10f,3,.99f),Glow,false).localRotation=pillar.localRotation;
            }
            Part("Eclipsed remnant moon",PrimitiveType.Sphere,Root,new Vector3(0,32,95),Vector3.one*16,Armor);
        }
        void LotusCourt()
        {
            Stone.SetColor("_BaseColor",new Color(.33f,.32f,.24f));
            RenderSettings.fogColor=new Color(.22f,.26f,.28f);RenderSettings.ambientSkyColor=new Color(.58f,.48f,.4f);
            for(int side=-1;side<=1;side+=2)
            {
                Box("Lotus reflecting water",new Vector3(side*20,.02f,25),new Vector3(4,.06f,31),Water,false);
                for(int i=0;i<6;i++)
                {
                    float z=11+i*5;Tree(new Vector3(side*26,0,z),1.7f);Lantern(new Vector3(side*18.2f,0,z),true);
                    var petal=Part("Lotus leaf",PrimitiveType.Cylinder,Root,new Vector3(side*20,.07f,z),new Vector3(1.4f,.025f,1.4f),Leaf);
                    Part("Lotus heart",PrimitiveType.Sphere,Root,new Vector3(side*20,.2f,z),new Vector3(.38f,.25f,.38f),Gold);
                }
            }
            Temple(new Vector3(0,0,55),17,9);Torii(new Vector3(0,0,44),10);
            Ring(new Vector3(-7,.05f,25),5,.065f,Gold);Ring(new Vector3(7,.05f,25),5,.065f,Steel);
            for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Box("Twin lotus inlay",new Vector3(Mathf.Sin(a)*12,.05f,25+Mathf.Cos(a)*12),new Vector3(.18f,.05f,2),Gold,false).localEulerAngles=new Vector3(0,i*45,0);}
        }
        void SwordTerrace()
        {
            Stone.SetColor("_BaseColor",new Color(.25f,.17f,.23f));
            RenderSettings.fogColor=new Color(.26f,.13f,.16f);RenderSettings.ambientSkyColor=new Color(.48f,.34f,.36f);
            for(int side=-1;side<=1;side+=2)
            {
                for(int z=8;z<=44;z+=9)
                {
                    Box("Sky colonnade",new Vector3(side*27,6,z),new Vector3(1.5f,12,1.5f),DarkStone,false);
                    Box("Vermilion standard",new Vector3(side*26,7,z),new Vector3(.08f,5.8f,3.5f),Crimson,false);
                    Box("High brass crossbar",new Vector3(side*27,12,z),new Vector3(4,.4f,2),Gold,false);
                    Lantern(new Vector3(side*22,0,z),true);
                }
            }
            Temple(new Vector3(0,0,56),18,11);
            for(int i=0;i<18;i++)
            {float a=i*Mathf.PI/9;var blade=Box("Memorial sword monolith",new Vector3(Mathf.Sin(a)*31,4,25+Mathf.Cos(a)*31),new Vector3(.45f,8,.16f),Steel,false);blade.localEulerAngles=new Vector3(0,i*20,-8);}
            for(int z=5;z<=45;z+=4)Box("Sword path inlay",new Vector3(0,.05f,z),new Vector3(.08f,.04f,2.5f),Crimson,false);
            Part("Distant red sun",PrimitiveType.Sphere,Root,new Vector3(0,35,110),Vector3.one*19,RedGlow);
        }
        void BallisticsHarbor()
        {
            Stone.SetColor("_BaseColor",new Color(.13f,.20f,.24f));
            RenderSettings.fogColor=new Color(.055f,.16f,.19f);RenderSettings.ambientSkyColor=new Color(.24f,.44f,.5f);
            Box("Harbor sea",new Vector3(0,-2.5f,25),new Vector3(180,.2f,180),Water,false);
            for(int side=-1;side<=1;side+=2)
            {
                for(int z=10;z<=50;z+=10)
                {
                    Box("Freight container",new Vector3(side*29,2,z),new Vector3(7,4,8),z%20==0?Crimson:DarkWood,false);
                    for(int j=-3;j<=3;j++)Box("Container rib",new Vector3(side*25.4f,2,z+j),new Vector3(.12f,3.7f,.10f),Steel,false);
                }
                Box("Crane tower",new Vector3(side*34,12,40),new Vector3(2,24,2),Gold,false);
                Box("Crane boom",new Vector3(side*24,24,40),new Vector3(25,1,1),Gold,false);
                Box("Suspended cable",new Vector3(side*15,18,40),new Vector3(.07f,12,.07f),Steel,false);
                for(int z=15;z<=35;z+=20)
                {
                    Box("Ballistic cover",new Vector3(side*10,1.05f,z),new Vector3(3,2.1f,2.4f),DarkStone);
                    Box("Cover luminous cap",new Vector3(side*10,2.12f,z),new Vector3(3.1f,.08f,2.5f),Glow,false);
                }
                for(int z=4;z<48;z+=4)Box("Runway stripe",new Vector3(side*3.5f,.06f,z),new Vector3(.14f,.04f,1.8f),Gold,false);
            }
            for(int i=-2;i<=2;i++)Box("Harbor skyline",new Vector3(i*13,7+Mathf.Abs(i)*2,65),new Vector3(8,14+Mathf.Abs(i)*4,6),DarkStone,false);
        }
        void GlacialForge()
        {
            RenderSettings.fogColor=new Color(.14f,.23f,.30f);RenderSettings.ambientSkyColor=new Color(.48f,.64f,.74f);
            Stone.SetColor("_BaseColor",new Color(.44f,.56f,.62f));Water.SetColor("_BaseColor",new Color(.16f,.38f,.47f));
            for(int i=0;i<24;i++)
            {
                float a=i*Mathf.PI/12;float r=31+(i%3)*3;var peak=MeshPart("Glacier pinnacle",cragMesh,new Vector3(Mathf.Sin(a)*r,-3,25+Mathf.Cos(a)*r),new Vector3(8+i%3,15+i%5*3,9),i%3==0?Steel:Stone);peak.localEulerAngles=new Vector3(0,i*39,0);
            }
            for(int side=-1;side<=1;side+=2)
            {
                Box("Forge channel",new Vector3(side*20,.045f,25),new Vector3(1.2f,.05f,39),RedGlow,false);
                for(int z=10;z<=40;z+=10)
                {
                    Part("Forge cauldron",PrimitiveType.Cylinder,Root,new Vector3(side*27,2,z),new Vector3(5,2,5),DarkStone);
                    Part("Molten heart",PrimitiveType.Cylinder,Root,new Vector3(side*27,4.03f,z),new Vector3(4.5f,.03f,4.5f),RedGlow);
                }
            }
            for(int i=0;i<8;i++){float a=i*45;var seam=Box("Radial forge seam",new Vector3(0,.055f,25),new Vector3(.08f,.04f,38),Gold,false);seam.localEulerAngles=new Vector3(0,a,0);}
            Ring(new Vector3(0,.06f,25),9,.12f,Steel);
        }
        void MoonObservatory()
        {
            RenderSettings.fogColor=new Color(.13f,.10f,.22f);RenderSettings.ambientSkyColor=new Color(.38f,.30f,.52f);
            Stone.SetColor("_BaseColor",new Color(.23f,.20f,.32f));
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6;Vector3 p=new Vector3(Mathf.Sin(a)*29,0,25+Mathf.Cos(a)*29);
                Box("Observatory marble column",p+Vector3.up*6,new Vector3(1.4f,12,1.4f),Stone,false);
                Box("Gilded column capital",p+Vector3.up*12,new Vector3(2.4f,.5f,2.4f),Gold,false);
                var arch=Box("Celestial lintel",p+Vector3.up*13,new Vector3(14,.55f,1),Gold,false);arch.localEulerAngles=new Vector3(0,-i*30,0);
                Part("Star lantern",PrimitiveType.Sphere,Root,p+Vector3.up*10,new Vector3(.55f,.55f,.55f),Glow);
            }
            for(int r=3;r<=15;r+=3)Ring(new Vector3(0,.06f+r*.0005f,25),r,.035f,Gold);
            for(int i=0;i<24;i++)
            {float a=i*Mathf.PI/12;var spoke=Box("Zodiac spoke",new Vector3(Mathf.Sin(a)*12,.07f,25+Mathf.Cos(a)*12),new Vector3(.04f,.025f,6),Gold,false);spoke.localEulerAngles=new Vector3(0,i*15,0);}
            Part("Observatory moon",PrimitiveType.Sphere,Root,new Vector3(-22,34,92),Vector3.one*17,Steel);
            var instrument=new GameObject("Great astrolabe").transform;instrument.SetParent(Root,false);instrument.position=new Vector3(0,10,58);
            for(int i=0;i<3;i++){var ring=Part("Astrolabe armillary",PrimitiveType.Cylinder,instrument,Vector3.zero,new Vector3(13-i*2,.06f,13-i*2),Gold);ring.localEulerAngles=new Vector3(20+i*28,i*40,20+i*25);}
        }
    }
}
