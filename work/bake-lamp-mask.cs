using UnityEngine;
using System.Collections.Generic;
var texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Golovomka/Resources/Art/PoliceStationLayout.png");
var previous = RenderTexture.active;
var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
Graphics.Blit(texture, rt); RenderTexture.active = rt;
const int left = 895, top = 505, width = 160, height = 95;
var crop = new Texture2D(width, height, TextureFormat.RGBA32, false);
crop.ReadPixels(new Rect(left, texture.height - top - height, width, height), 0, 0); crop.Apply();
var pixels = crop.GetPixels(); var solid = new bool[width*height]; var outside = new bool[solid.Length];
for (int i=0; i<solid.Length; i++)
{
    var c = pixels[i]; float high = Mathf.Max(c.r, Mathf.Max(c.g,c.b));
    solid[i] = high < .40f || (c.r-c.b > .25f && (c.r-c.b)/Mathf.Max(c.r,.001f) > .40f);
}
// Заполняем светлые блики внутри замкнутого тёмного контура лампы.
var queue = new Queue<int>();
void Add(int x,int y) { if(x<0||x>=width||y<0||y>=height)return; int n=y*width+x; if(solid[n]||outside[n])return; outside[n]=true; queue.Enqueue(n); }
for(int x=0;x<width;x++){Add(x,0);Add(x,height-1);} for(int y=0;y<height;y++){Add(0,y);Add(width-1,y);}
while(queue.Count>0){int n=queue.Dequeue(),x=n%width,y=n/width;Add(x-1,y);Add(x+1,y);Add(x,y-1);Add(x,y+1);}
var text = new System.Text.StringBuilder("# Pixel rows of lamp above desk. Source: PoliceStationLayout.png 1586x992. y,xBegin,xEndExclusive\n");
int count=0;
for(int y=0;y<height;y++)
{
    int row=height-1-y;
    for(int x=0;x<width;)
    {
        bool Include(int column)
        {
            int n=row*width+column, sourceX=left+column, sourceY=top+y;
            // Блики внутри абажура замкнуты контуром. Под абажуром —
            // свечение на полу, а в зазоре между дугой и стойкой — сам пол.
            if(sourceY>=568) return sourceX>=1002 && sourceX<1020;
            return solid[n] || (!outside[n] && sourceX<987);
        }
        if(!Include(x)){x++;continue;} int begin=x;
        while(x<width&&Include(x))x++;
        text.Append(top+y).Append(',').Append(left+begin).Append(',').Append(left+x).Append('\n'); count++;
    }
}
System.IO.File.WriteAllText("Assets/Golovomka/Resources/Art/PoliceLampMask.txt",text.ToString());
RenderTexture.active=previous; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(crop);
UnityEditor.AssetDatabase.Refresh(); return new { spans=count };

