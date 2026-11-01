using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ConGame.Editor
{
    /// <summary>Deterministic, editable stand-ins for details missing from the delivered art.</summary>
    static class MvMainShotAssets
    {
        internal const string Root = "Assets/_Game/Art/MainShot_20261101";
        static readonly Color Paper = new Color(.86f, .81f, .70f);
        static readonly Color Leaf = new Color(.28f, .31f, .18f);
        static readonly Color Rose = new Color(.43f, .19f, .16f);

        internal static void Prepare()
        {
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
        }

        internal static Material Material(string name, Color color, float smoothness = .2f, Texture texture = null)
        {
            string path = Root + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(m);
            return m;
        }

        internal static Texture2D Wallpaper()
        {
            const int size = 1024;
            var rng = new System.Random(1101);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float age = .015f * Mathf.PerlinNoise(x / 97f, y / 111f) + (float)rng.NextDouble() * .018f;
                    pixels[y * size + x] = Paper * (1 - age);
                }
            // Small botanical sprigs with staggered repeats, rather than isolated large dots.
            for (int row = -1; row < 9; row++)
                for (int col = -1; col < 9; col++)
                {
                    float cx = col * 128 + 59 + (row % 2) * 64;
                    float cy = row * 128 + 58;
                    float lean = ((col + row) % 2 == 0) ? 1 : -1;
                    Line(pixels, size, new Vector2(cx - 8 * lean, cy - 28), new Vector2(cx + 9 * lean, cy + 22), Leaf, 1);
                    for (int branch = 0; branch < 5; branch++)
                    {
                        float yy = cy - 20 + branch * 9;
                        float xx = cx + (yy - cy) * .30f * lean;
                        float side = branch % 2 == 0 ? 1 : -1;
                        Line(pixels, size, new Vector2(xx, yy), new Vector2(xx + side * 16, yy + 10), Leaf, 1);
                        Ellipse(pixels, size, new Vector2(xx + side * 12, yy + 7), new Vector2(10, 3.5f), side * .55f, Leaf);
                        Ellipse(pixels, size, new Vector2(xx + side * 20, yy + 13), new Vector2(7, 2.5f), side * .8f, Leaf * .84f);
                    }
                    Flower(pixels, size, cx + 5 * lean, cy + 20, 8.5f);
                    Flower(pixels, size, cx - 11 * lean, cy + 3, 7);
                    Flower(pixels, size, cx + 17 * lean, cy - 12, 5.2f);
                }
            return SaveTexture("FloralWallpaper", pixels, size, size, TextureWrapMode.Repeat);
        }

        static void Flower(Color[] p, int n, float x, float y, float r)
        {
            for (int petal = 0; petal < 6; petal++)
            {
                float a = petal * Mathf.PI / 3;
                Ellipse(p, n, new Vector2(x + Mathf.Cos(a) * r * .42f, y + Mathf.Sin(a) * r * .42f),
                    new Vector2(r * .68f, r * .57f), a, Color.Lerp(Rose, Paper, petal % 2 * .14f));
            }
            Ellipse(p, n, new Vector2(x, y), new Vector2(r * .46f, r * .46f), 0, Rose * .7f);
            Ellipse(p, n, new Vector2(x - r * .12f, y + r * .10f), new Vector2(r * .24f, r * .16f), .3f, new Color(.64f, .39f, .27f));
        }

        internal static Texture2D Carpet()
        {
            const int n = 512;
            var rng = new System.Random(641);
            var p = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float pile = (float)rng.NextDouble() * .12f + Mathf.PerlinNoise(x * .07f, y * .07f) * .08f;
                    p[y * n + x] = new Color(.125f, .22f, .145f) * (.84f + pile);
                }
            return SaveTexture("ForestCarpet", p, n, n, TextureWrapMode.Repeat);
        }

        internal static Texture2D Painting()
        {
            const int n = 512;
            var p = new Color[n * n];
            var rng = new System.Random(83);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float cloud = Mathf.PerlinNoise(x * .013f, y * .012f) * .10f;
                    p[y * n + x] = new Color(.08f + cloud, .065f + cloud * .62f, .045f + cloud * .43f);
                }
            Color top = new Color(.53f, .42f, .27f), side = new Color(.26f, .22f, .15f), shade = new Color(.13f, .12f, .10f);
            // An original impossible architectural loop, echoing the reference painting's dark/gold planes.
            Poly(p, n, top, new Vector2(76, 330), new Vector2(284, 433), new Vector2(444, 333), new Vector2(386, 298), new Vector2(278, 363), new Vector2(139, 294));
            Poly(p, n, side, new Vector2(76,330),new Vector2(139,294),new Vector2(139,238),new Vector2(76,273));
            Poly(p, n, shade,new Vector2(139,294),new Vector2(278,363),new Vector2(278,307),new Vector2(139,238));
            Poly(p, n, top,new Vector2(139,238),new Vector2(278,170),new Vector2(386,237),new Vector2(444,201),new Vector2(282,100),new Vector2(76,203));
            Poly(p, n, side,new Vector2(76,203),new Vector2(282,100),new Vector2(282,54),new Vector2(76,157));
            Poly(p, n, side,new Vector2(386,298),new Vector2(444,333),new Vector2(444,201),new Vector2(386,166));
            Poly(p, n, shade,new Vector2(282,100),new Vector2(444,201),new Vector2(444,155),new Vector2(282,54));
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float age = .90f + (float)rng.NextDouble() * .17f;
                    float edge = 1 - .3f * Mathf.Pow(Vector2.Distance(new Vector2(x,y), new Vector2(256,256)) / 362, 2);
                    p[y*n+x] *= age * edge;
                }
            return SaveTexture("DarkArchitecturalPainting", p, n, n, TextureWrapMode.Clamp);
        }

        static Texture2D SaveTexture(string name, Color[] p, int w, int h, TextureWrapMode wrap)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGB24, true);
            tex.SetPixels(p); tex.Apply();
            string path = Root + "/" + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = wrap;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void Ellipse(Color[] p, int n, Vector2 center, Vector2 radius, float angle, Color color)
        {
            int extent = Mathf.CeilToInt(Mathf.Max(radius.x, radius.y)) + 1;
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            for (int y = (int)center.y - extent; y <= center.y + extent; y++)
                for (int x = (int)center.x - extent; x <= center.x + extent; x++)
                {
                    float dx = x - center.x, dy = y - center.y;
                    float u = (dx*c+dy*s)/radius.x, v = (-dx*s+dy*c)/radius.y;
                    float d = u*u+v*v;
                    if (d < 1) p[((y % n + n) % n)*n+(x % n + n) % n] = color * (.9f + .1f * (1-d));
                }
        }

        static void Line(Color[] p, int n, Vector2 a, Vector2 b, Color c, float width)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(a,b));
            for (int i=0;i<=steps;i++) Ellipse(p,n,Vector2.Lerp(a,b,(float)i/Mathf.Max(steps,1)),Vector2.one*width,0,c);
        }

        static void Poly(Color[] p, int n, Color c, params Vector2[] vertices)
        {
            for (int y=0;y<n;y++) for (int x=0;x<n;x++)
            {
                bool inside=false;
                for (int i=0,j=vertices.Length-1;i<vertices.Length;j=i++)
                    if ((vertices[i].y>y)!=(vertices[j].y>y) && x<(vertices[j].x-vertices[i].x)*(y-vertices[i].y)/(vertices[j].y-vertices[i].y)+vertices[i].x) inside=!inside;
                if (inside) p[y*n+x]=c;
            }
        }

        internal static Mesh UpholsteryMesh()
        {
            string path=Root+"/BevelledUpholstery.asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing!=null) return existing;
            var verts=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
            Vector3[] normals={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            const int steps=10;
            foreach(var normal in normals)
            {
                Vector3 tangent=Vector3.Cross(normal,Mathf.Abs(normal.y)>.5f?Vector3.forward:Vector3.up).normalized;
                Vector3 bitangent=Vector3.Cross(normal,tangent);
                int start=verts.Count;
                for(int y=0;y<=steps;y++) for(int x=0;x<=steps;x++)
                {
                    Vector3 cube=normal*.5f+tangent*((float)x/steps-.5f)+bitangent*((float)y/steps-.5f);
                    Vector3 inner=new Vector3(Mathf.Clamp(cube.x,-.38f,.38f),Mathf.Clamp(cube.y,-.38f,.38f),Mathf.Clamp(cube.z,-.38f,.38f));
                    verts.Add(inner+(cube-inner).normalized*.12f);uv.Add(new Vector2((float)x/steps,(float)y/steps));
                    if(x<steps&&y<steps)
                    {
                        int i=start+y*(steps+1)+x;
                        indices.Add(i);indices.Add(i+1);indices.Add(i+steps+1);
                        indices.Add(i+1);indices.Add(i+steps+2);indices.Add(i+steps+1);
                    }
                }
            }
            var mesh=new Mesh {name="BevelledUpholstery"};
            mesh.SetVertices(verts);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }

        internal static Mesh Surface(string name, int nx, int ny, Func<float,float,Vector3> point)
        {
            var v = new Vector3[(nx+1)*(ny+1)];
            var uv = new Vector2[v.Length];
            var indices = new List<int>(nx*ny*6);
            for (int y=0;y<=ny;y++) for (int x=0;x<=nx;x++)
            {
                int i=y*(nx+1)+x;
                uv[i]=new Vector2((float)x/nx,(float)y/ny);
                v[i]=point(uv[i].x,uv[i].y);
                if (x<nx && y<ny)
                {
                    indices.Add(i);indices.Add(i+nx+1);indices.Add(i+1);
                    indices.Add(i+1);indices.Add(i+nx+1);indices.Add(i+nx+2);
                }
            }
            var mesh = new Mesh { name=name };
            mesh.vertices=v;mesh.uv=uv;mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            string path=Root+"/"+name+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing==null) AssetDatabase.CreateAsset(mesh,path);
            else { EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing; }
            EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
