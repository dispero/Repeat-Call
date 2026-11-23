using UnityEngine;
using UnityEngine.UI;

namespace Golovomka
{
    sealed class ComicPanel : MaskableGraphic
    {
        const float Border = 3f;
        const float Radius = 17f;
        const int CornerSteps = 7;
        static readonly Color32 Ink = Color.black;
        static readonly Color32 Paper = Color.white;
        Vector2 tailTip;
        bool tailAbove;
        bool hasTail;

        public void PointAt(Vector2 tip, bool above)
        {
            if (hasTail && tip == tailTip && above == tailAbove) return;
            hasTail = true;
            tailTip = tip;
            tailAbove = above;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            float baseX = Mathf.Clamp(tailTip.x, rect.xMin + Radius + 13f, rect.xMax - Radius - 13f);
            float edge = tailAbove ? rect.yMax : rect.yMin;
            float inward = tailAbove ? -1f : 1f;
            if (hasTail)
                Triangle(mesh, new Vector2(baseX - 13f, edge + inward),
                    new Vector2(baseX + 13f, edge + inward), tailTip, Paper);

            RoundedRect(mesh, rect, Radius, Paper);
            rect = Rect.MinMaxRect(rect.xMin + Border, rect.yMin + Border, rect.xMax - Border, rect.yMax - Border);
            RoundedRect(mesh, rect, Radius - Border, Ink);

            if (hasTail)
                Triangle(mesh, new Vector2(baseX - 10f, edge + inward * (Border + 1f)),
                    new Vector2(baseX + 10f, edge + inward * (Border + 1f)),
                    Vector2.MoveTowards(tailTip, new Vector2(baseX, edge), 7f), Ink);
        }

        static void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color32 color)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(a, color, Vector2.zero);
            mesh.AddVert(b, color, Vector2.zero);
            mesh.AddVert(c, color, Vector2.zero);
            float winding = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            mesh.AddTriangle(first, winding < 0f ? first + 1 : first + 2, winding < 0f ? first + 2 : first + 1);
        }

        static void RoundedRect(VertexHelper mesh, Rect rect, float radius, Color32 color)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(rect.center, color, Vector2.zero);
            for (int corner = 0; corner < 4; corner++)
            {
                float x = corner == 0 || corner == 3 ? rect.xMax - radius : rect.xMin + radius;
                float y = corner < 2 ? rect.yMax - radius : rect.yMin + radius;
                for (int step = 0; step <= CornerSteps; step++)
                {
                    float angle = (corner + step / (float)CornerSteps) * Mathf.PI * .5f;
                    var point = new Vector2(x + radius * Mathf.Cos(angle), y + radius * Mathf.Sin(angle));
                    mesh.AddVert(point, color, Vector2.zero);
                }
            }
            int last = mesh.currentVertCount - 1;
            for (int vertex = first + 1; vertex < last; vertex++)
                mesh.AddTriangle(first, vertex + 1, vertex);
            mesh.AddTriangle(first, first + 1, last);
        }
    }
}
