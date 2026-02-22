using System.Numerics;

namespace DrpToDae.Formats.NUD
{
    public class Mesh
    {
        public enum BoneFlags
        {
            NotRigged = 0,
            Rigged = 4,
            SingleBind = 8
        }

        public string Name { get; set; } = "Mesh";
        public int boneflag = (int)BoneFlags.Rigged;
        public short singlebind = -1;
        public bool billboardY = false;
        public bool billboard = false;
        public bool useNsc = false;

        public bool sortByObjHierarchy = true;
        public float[] boundingSphere = new float[4];
        public float sortBias = 0;
        public float sortingDistance = 0;

        public List<Polygon> Polygons { get; set; } = new List<Polygon>();

        public Mesh()
        {
        }

        public Vector4 BoundingSphereVec
        {
            get { return new Vector4(boundingSphere[0], boundingSphere[1], boundingSphere[2], boundingSphere[3]); }
        }

        public void SetMeshAttributesFromName()
        {
            billboard = Name.Contains("BILLBOARD");
            billboardY = Name.Contains("BILLBOARDYAXIS");
            useNsc = Name.Contains("NSC");
            sortByObjHierarchy = Name.Contains("HIR");
        }
    }
}
