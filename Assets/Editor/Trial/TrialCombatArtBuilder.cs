using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TomorinTrial.Editor
{
    public static class TrialCombatArtBuilder
    {
        const string Root="Assets/Art/Materials/Resources/Trial/VFX/";
        const string MeshRoot="Assets/Art/Models/Resources/Trial/VFX/";
        [MenuItem("Tools/Tomorin/Build original combat VFX assets")]
        public static void Build()
        {
            System.IO.Directory.CreateDirectory(Root);System.IO.Directory.CreateDirectory(MeshRoot);AssetDatabase.Refresh();
            Material("TideSigil","Tomorin/Tide Sigil",Color.white,0);
            Material("TideRibbon","Tomorin/Tide Energy",Color.white,0);
            Material("TideShell","Tomorin/Tide Energy",new Color(.08f,.64f,1,.85f),1);
            Material("FrostIce","Universal Render Pipeline/Lit",new Color(.045f,.3f,.4f),0);
            var ice=AssetDatabase.LoadAssetAtPath<Material>(Root+"FrostIce.mat");
            ice.SetFloat("_Metallic",.45f);ice.SetFloat("_Smoothness",.8f);
            ice.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;
            ice.SetColor("_EmissionColor",new Color(.08f,1.1f,1.6f));ice.EnableKeyword("_EMISSION");EditorUtility.SetDirty(ice);
            Save(Shell(),"TidePearl");Save(Crown(),"TideCrown");
            AssetDatabase.SaveAssets();
            Debug.Log("Original tidal shell, crown meshes and three VFX materials built.");
        }
        static void Material(string name,string shaderName,Color tint,float mode)
        {
            var shader=Shader.Find(shaderName);if(!shader)throw new System.InvalidOperationException("Missing shader "+shaderName);
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Root+name+".mat");
            if(!mat){mat=new Material(shader){name=name};AssetDatabase.CreateAsset(mat,Root+name+".mat");}
            mat.shader=shader;if(mat.HasProperty("_BaseColor"))mat.SetColor("_BaseColor",tint);
            if(mat.HasProperty("_Mode"))mat.SetFloat("_Mode",mode);EditorUtility.SetDirty(mat);
        }
        static void Save(Mesh mesh,string name)
        {
            mesh.name=name;var old=AssetDatabase.LoadAssetAtPath<Mesh>(MeshRoot+name+".asset");
            if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);}
            else AssetDatabase.CreateAsset(mesh,MeshRoot+name+".asset");
        }
        static Mesh Shell()
        {
            const int rows=20,columns=48;var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
            {
                float v=(float)y/rows,u=(float)x/columns,a=u*Mathf.PI*2,p=v*Mathf.PI;
                float r=.5f*(1+.055f*Mathf.Sin(a*12+p*7)*Mathf.Sin(p));
                vertices.Add(new Vector3(Mathf.Sin(p)*Mathf.Cos(a),Mathf.Cos(p),Mathf.Sin(p)*Mathf.Sin(a))*r);uv.Add(new Vector2(u,v));
                if(y<rows && x<columns){int k=y*(columns+1)+x;triangles.AddRange(new[]{k,k+columns+1,k+1,k+1,k+columns+1,k+columns+2});}
            }
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static Mesh Crown()
        {
            const int segments=96;var v=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
            for(int i=0;i<=segments;i++)
            {
                float a=(float)i/segments*Mathf.PI*2;float spike=.22f+.48f*Mathf.Pow(.5f+.5f*Mathf.Sin(a*24),4);
                for(int j=0;j<3;j++)
                {
                    float radius=j==0?.72f:j==1?1:1.08f;
                    v.Add(new Vector3(Mathf.Sin(a)*radius,j==1?spike:0,Mathf.Cos(a)*radius));uv.Add(new Vector2((float)i/segments,j*.5f));
                }
                if(i<segments)for(int j=0;j<2;j++){int k=i*3+j;tris.AddRange(new[]{k,k+3,k+1,k+1,k+3,k+4});}
            }
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
