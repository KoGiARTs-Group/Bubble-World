using UnityEngine;

namespace BubbleWorld
{
    public static class PrimitiveMeshUtility
    {
        private static Mesh cachedCube;
        private static Mesh cachedSphere;
        private static Mesh cachedCylinder;
        private static Mesh cachedCapsule;
        private static Mesh cachedPlane;
        private static Mesh cachedQuad;
        private static Mesh cachedEdgeRamp;
        private static Mesh cachedOuterCorner;
        private static Mesh cachedInnerCorner;
        private static Mesh cachedHalfSlab;
        private static Mesh cachedWaterStraight;
        private static Mesh cachedWaterCorner;
        private static Mesh cachedWaterTee;
        private static Mesh cachedWaterCross;
        private static Mesh cachedHalfRamp;
        private static Mesh cachedHalfWedge;
        private static Mesh cachedHalfOuter;
        private static Mesh cachedAsymmetricRamp;
        private static Mesh cachedAsymmetricOuterCorner;
        private static Mesh cachedAsymmetricInnerCorner;
        private static Mesh cachedHalfVerticalFront;
        private static Mesh cachedHalfVerticalBack;
        private static Mesh cachedHalfVerticalLeft;
        private static Mesh cachedHalfVerticalRight;
        private static Mesh cachedRoundedEdge;

        public static Mesh Cube => cachedCube != null ? cachedCube : (cachedCube = BuildCube());
        public static Mesh Sphere => cachedSphere != null ? cachedSphere : (cachedSphere = BuildSphere());
        public static Mesh Cylinder => cachedCylinder != null ? cachedCylinder : (cachedCylinder = BuildCylinder());
        public static Mesh Capsule => cachedCapsule != null ? cachedCapsule : (cachedCapsule = BuildCapsule());
        public static Mesh Plane => cachedPlane != null ? cachedPlane : (cachedPlane = BuildPlane());
        public static Mesh Quad => cachedQuad != null ? cachedQuad : (cachedQuad = BuildQuad());
        public static Mesh EdgeRamp => cachedEdgeRamp != null ? cachedEdgeRamp : (cachedEdgeRamp = BuildEdgeRamp());
        public static Mesh OuterCorner => cachedOuterCorner != null ? cachedOuterCorner : (cachedOuterCorner = BuildOuterCorner());
        public static Mesh InnerCorner => cachedInnerCorner != null ? cachedInnerCorner : (cachedInnerCorner = BuildInnerCorner());
        public static Mesh HalfSlab => cachedHalfSlab != null ? cachedHalfSlab : (cachedHalfSlab = BuildHalfSlab());
        public static Mesh WaterStraight => cachedWaterStraight != null ? cachedWaterStraight : (cachedWaterStraight = BuildWaterStraight());
        public static Mesh WaterCorner => cachedWaterCorner != null ? cachedWaterCorner : (cachedWaterCorner = BuildWaterCorner());
        public static Mesh WaterTee => cachedWaterTee != null ? cachedWaterTee : (cachedWaterTee = BuildWaterTee());
        public static Mesh WaterCross => cachedWaterCross != null ? cachedWaterCross : (cachedWaterCross = BuildWaterCross());
        public static Mesh HalfRamp => cachedHalfRamp != null ? cachedHalfRamp : (cachedHalfRamp = BuildHalfRamp());
        public static Mesh HalfWedge => cachedHalfWedge != null ? cachedHalfWedge : (cachedHalfWedge = BuildHalfWedge());
        public static Mesh HalfOuter => cachedHalfOuter != null ? cachedHalfOuter : (cachedHalfOuter = BuildHalfOuter());
        public static Mesh AsymmetricRamp => cachedAsymmetricRamp != null ? cachedAsymmetricRamp : (cachedAsymmetricRamp = BuildAsymmetricRamp());
        public static Mesh AsymmetricOuterCorner => cachedAsymmetricOuterCorner != null ? cachedAsymmetricOuterCorner : (cachedAsymmetricOuterCorner = BuildAsymmetricOuterCorner());
        public static Mesh AsymmetricInnerCorner => cachedAsymmetricInnerCorner != null ? cachedAsymmetricInnerCorner : (cachedAsymmetricInnerCorner = BuildAsymmetricInnerCorner());
        public static Mesh HalfVerticalFront => cachedHalfVerticalFront != null ? cachedHalfVerticalFront : (cachedHalfVerticalFront = BuildHalfVerticalFront());
        public static Mesh HalfVerticalBack => cachedHalfVerticalBack != null ? cachedHalfVerticalBack : (cachedHalfVerticalBack = BuildHalfVerticalBack());
        public static Mesh HalfVerticalLeft => cachedHalfVerticalLeft != null ? cachedHalfVerticalLeft : (cachedHalfVerticalLeft = BuildHalfVerticalLeft());
        public static Mesh HalfVerticalRight => cachedHalfVerticalRight != null ? cachedHalfVerticalRight : (cachedHalfVerticalRight = BuildHalfVerticalRight());
        public static Mesh RoundedEdge => cachedRoundedEdge != null ? cachedRoundedEdge : (cachedRoundedEdge = BuildRoundedEdge());

        public static Mesh GetVariantMesh(TileVariant variant)
        {
            switch (variant)
            {
                case TileVariant.Center:        return Cube;
                case TileVariant.Isolated:      return Cube;
                case TileVariant.EdgeNorth:
                case TileVariant.EdgeSouth:
                case TileVariant.EdgeEast:
                case TileVariant.EdgeWest:      return EdgeRamp;
                case TileVariant.OuterCornerNE:
                case TileVariant.OuterCornerNW:
                case TileVariant.OuterCornerSE:
                case TileVariant.OuterCornerSW: return OuterCorner;
                case TileVariant.InnerCornerNE:
                case TileVariant.InnerCornerNW:
                case TileVariant.InnerCornerSE:
                case TileVariant.InnerCornerSW: return InnerCorner;
                case TileVariant.HalfCenter:
                case TileVariant.HalfIsolated:  return HalfSlab;
                case TileVariant.HalfEdgeNorth:
                case TileVariant.HalfEdgeSouth:
                case TileVariant.HalfEdgeEast:
                case TileVariant.HalfEdgeWest:  return HalfRamp;
                case TileVariant.HalfOuterCornerNE:
                case TileVariant.HalfOuterCornerNW:
                case TileVariant.HalfOuterCornerSE:
                case TileVariant.HalfOuterCornerSW: return HalfOuter;
                case TileVariant.HalfInnerCornerNE:
                case TileVariant.HalfInnerCornerNW:
                case TileVariant.HalfInnerCornerSE:
                case TileVariant.HalfInnerCornerSW: return HalfWedge;
                case TileVariant.FullToHalfEdgeNorth:
                case TileVariant.FullToHalfEdgeSouth:
                case TileVariant.FullToHalfEdgeEast:
                case TileVariant.FullToHalfEdgeWest: return AsymmetricRamp;
                case TileVariant.FullToHalfOuterCornerNE:
                case TileVariant.FullToHalfOuterCornerNW:
                case TileVariant.FullToHalfOuterCornerSE:
                case TileVariant.FullToHalfOuterCornerSW: return AsymmetricOuterCorner;
                case TileVariant.FullToHalfInnerCornerNE:
                case TileVariant.FullToHalfInnerCornerNW:
                case TileVariant.FullToHalfInnerCornerSE:
                case TileVariant.FullToHalfInnerCornerSW: return AsymmetricInnerCorner;
                case TileVariant.HalfVerticalFront: return HalfVerticalFront;
                case TileVariant.HalfVerticalBack: return HalfVerticalBack;
                case TileVariant.HalfVerticalLeft: return HalfVerticalLeft;
                case TileVariant.HalfVerticalRight: return HalfVerticalRight;
                case TileVariant.RoundedEdgeNorth:
                case TileVariant.RoundedEdgeSouth:
                case TileVariant.RoundedEdgeEast:
                case TileVariant.RoundedEdgeWest: return RoundedEdge;
                case TileVariant.WaterStraight: return WaterStraight;
                case TileVariant.WaterCorner:   return WaterCorner;
                case TileVariant.WaterTee:      return WaterTee;
                case TileVariant.WaterCross:    return WaterCross;
                default:                       return Cube;
            }
        }

        private static Mesh BuildCube()
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh mesh = Object.Instantiate(temp.GetComponent<MeshFilter>().sharedMesh);
            mesh.name = "PrimCube";
            Object.DestroyImmediate(temp);
            return mesh;
        }

        private static Mesh BuildSphere()
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh mesh = Object.Instantiate(temp.GetComponent<MeshFilter>().sharedMesh);
            mesh.name = "PrimSphere";
            Object.DestroyImmediate(temp);
            return mesh;
        }

        private static Mesh BuildCylinder()
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Mesh mesh = Object.Instantiate(temp.GetComponent<MeshFilter>().sharedMesh);
            mesh.name = "PrimCylinder";
            Object.DestroyImmediate(temp);
            return mesh;
        }

        private static Mesh BuildCapsule()
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Mesh mesh = Object.Instantiate(temp.GetComponent<MeshFilter>().sharedMesh);
            mesh.name = "PrimCapsule";
            Object.DestroyImmediate(temp);
            return mesh;
        }

        private static Mesh BuildPlane()
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Mesh mesh = Object.Instantiate(temp.GetComponent<MeshFilter>().sharedMesh);
            mesh.name = "PrimPlane";
            Object.DestroyImmediate(temp);
            return mesh;
        }

        private static Mesh BuildQuad()
        {
            Mesh mesh = new Mesh { name = "PrimQuad" };
            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f)
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 1), new Vector2(1, 1)
            };
            mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh BuildEdgeRamp()
        {
            Mesh mesh = new Mesh { name = "PrimEdgeRamp" };
            float h = 0.5f;
            Vector3[] v = new Vector3[6];
            v[0] = new Vector3(-h, -h,  h);
            v[1] = new Vector3( h, -h,  h);
            v[2] = new Vector3( h, -h, -h);
            v[3] = new Vector3(-h, -h, -h);
            v[4] = new Vector3(-h,  h, -h);
            v[5] = new Vector3( h,  h, -h);
            mesh.vertices = v;
            mesh.triangles = new int[]
            {
                0, 1, 2, 0, 2, 3,
                0, 4, 5, 0, 5, 1,
                0, 3, 4,
                1, 5, 2,
                4, 3, 2, 4, 2, 5
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0,0), new Vector2(1,0), new Vector2(1,1),
                new Vector2(0,1), new Vector2(0,0), new Vector2(1,0)
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh BuildOuterCorner()
        {
            Mesh mesh = new Mesh { name = "PrimOuterCorner" };
            float h = 0.5f;
            Vector3[] v = new Vector3[5];
            v[0] = new Vector3(-h, -h, -h);
            v[1] = new Vector3( h, -h, -h);
            v[2] = new Vector3( h, -h,  h);
            v[3] = new Vector3(-h, -h,  h);
            v[4] = new Vector3(-h,  h, -h);
            mesh.vertices = v;
            mesh.triangles = new int[]
            {
                0, 1, 2, 0, 2, 3,
                4, 0, 1,
                4, 1, 2,
                4, 2, 3,
                4, 3, 0
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh BuildInnerCorner()
        {
            Mesh mesh = new Mesh { name = "PrimInnerCorner" };
            float h = 0.5f;
            Vector3[] v = new Vector3[5];
            v[0] = new Vector3(-h, -h, -h);
            v[1] = new Vector3( h, -h, -h);
            v[2] = new Vector3( h, -h,  h);
            v[3] = new Vector3(-h, -h,  h);
            v[4] = new Vector3( h,  h,  h);
            mesh.vertices = v;
            mesh.triangles = new int[]
            {
                0, 1, 2, 0, 2, 3,
                4, 0, 1,
                4, 1, 2,
                4, 2, 3,
                4, 3, 0
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh BuildHalfSlab()
        {
            Mesh mesh = new Mesh { name = "PrimHalfSlab" };
            float h = 0.5f;
            Vector3[] v = new Vector3[8];
            v[0] = new Vector3(-h, -h, -h);
            v[1] = new Vector3( h, -h, -h);
            v[2] = new Vector3( h, -h,  h);
            v[3] = new Vector3(-h, -h,  h);
            v[4] = new Vector3(-h,  0, -h);
            v[5] = new Vector3( h,  0, -h);
            v[6] = new Vector3( h,  0,  h);
            v[7] = new Vector3(-h,  0,  h);
            mesh.vertices = v;
            mesh.triangles = new int[]
            {
                0, 1, 2, 0, 2, 3,
                4, 7, 6, 4, 6, 5,
                0, 4, 5, 0, 5, 1,
                1, 5, 6, 1, 6, 2,
                2, 6, 7, 2, 7, 3,
                3, 7, 4, 3, 4, 0
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh BuildHalfRamp()
        {
            Mesh mesh = new Mesh { name = "PrimHalfRamp" };
            float h = 0.5f;
            Vector3[] v = new Vector3[6];
            v[0] = new Vector3(-h, -h,  h);
            v[1] = new Vector3( h, -h,  h);
            v[2] = new Vector3( h, -h, -h);
            v[3] = new Vector3(-h, -h, -h);
            v[4] = new Vector3(-h,  0, -h);
            v[5] = new Vector3( h,  0, -h);
            mesh.vertices = v;
            mesh.triangles = new int[]
            {
                0, 1, 2, 0, 2, 3,
                0, 4, 5, 0, 5, 1,
                0, 3, 4,
                1, 5, 2,
                4, 3, 2, 4, 2, 5
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh BuildHalfWedge()
        {
            Mesh mesh = new Mesh { name = "PrimHalfWedge" };
            float h = 0.5f;
            Vector3[] v = new Vector3[5];
            v[0] = new Vector3(-h, -h, -h);
            v[1] = new Vector3( h, -h, -h);
            v[2] = new Vector3( h, -h,  h);
            v[3] = new Vector3(-h, -h,  h);
            v[4] = new Vector3( h,  0,  h);
            mesh.vertices = v;
            mesh.triangles = new int[]
            {
                0, 1, 2, 0, 2, 3,
                4, 0, 1,
                4, 1, 2,
                4, 2, 3,
                4, 3, 0
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh BuildHalfOuter()
        {
            Mesh mesh = new Mesh { name = "PrimHalfOuter" };
            float h = 0.5f;
            Vector3[] v = new Vector3[5];
            v[0] = new Vector3(-h, -h, -h);
            v[1] = new Vector3( h, -h, -h);
            v[2] = new Vector3( h, -h,  h);
            v[3] = new Vector3(-h, -h,  h);
            v[4] = new Vector3(-h,  0, -h);
            mesh.vertices = v;
            mesh.triangles = new int[]
            {
                0, 1, 2, 0, 2, 3,
                4, 0, 1,
                4, 1, 2,
                4, 2, 3,
                4, 3, 0
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh BuildAsymmetricRamp()
        {
            Mesh mesh = new Mesh { name = "PrimAsymmetricRamp" };
            float h = 0.5f;
            Vector3[] v = new Vector3[8];
            v[0] = new Vector3(-h, -h,  h);
            v[1] = new Vector3( h, -h,  h);
            v[2] = new Vector3( h, -h, -h);
            v[3] = new Vector3(-h, -h, -h);
            v[4] = new Vector3(-h,  0, -h);
            v[5] = new Vector3( h,  0, -h);
            v[6] = new Vector3(-h,  h,  h);
            v[7] = new Vector3( h,  h,  h);
            mesh.vertices = v;
            mesh.triangles = new int[]
            {
                0, 1, 2, 0, 2, 3,
                0, 6, 7, 0, 7, 1,
                3, 2, 5, 3, 5, 4,
                0, 3, 4, 0, 4, 6,
                1, 7, 5, 1, 5, 2,
                6, 4, 5, 6, 5, 7
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh BuildWaterStraight()
        {
            Mesh mesh = new Mesh { name = "PrimWaterStraight" };
            Vector3[] v = new Vector3[8];
            v[0] = new Vector3(-0.5f, 0f, -0.25f);
            v[1] = new Vector3( 0.5f, 0f, -0.25f);
            v[2] = new Vector3( 0.5f, 0f,  0.25f);
            v[3] = new Vector3(-0.5f, 0f,  0.25f);
            v[4] = new Vector3(-0.5f, 0f, -0.25f);
            v[5] = new Vector3( 0.5f, 0f, -0.25f);
            v[6] = new Vector3( 0.5f, 0f,  0.25f);
            v[7] = new Vector3(-0.5f, 0f,  0.25f);
            mesh.vertices = v;
            mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh BuildWaterCorner()
        {
            Mesh mesh = new Mesh { name = "PrimWaterCorner" };
            Vector3[] v = new Vector3[6]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0.5f, 0f, 0f),
                new Vector3(0.5f, 0f, 0.5f),
                new Vector3(0f, 0f, 0.5f),
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 0f)
            };
            mesh.vertices = v;
            mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh BuildWaterTee()
        {
            Mesh mesh = new Mesh { name = "PrimWaterTee" };
            Vector3[] v = new Vector3[6]
            {
                new Vector3(-0.25f, 0f, -0.25f),
                new Vector3( 0.25f, 0f, -0.25f),
                new Vector3( 0.25f, 0f,  0.25f),
                new Vector3(-0.25f, 0f,  0.25f),
                new Vector3( 0f, 0f, -0.5f),
                new Vector3( 0f, 0f,  0f)
            };
            mesh.vertices = v;
            mesh.triangles = new int[] { 0, 1, 5, 1, 2, 5, 2, 3, 5, 3, 0, 5, 1, 4, 0 };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh BuildWaterCross()
        {
            Mesh mesh = new Mesh { name = "PrimWaterCross" };
            Vector3[] v = new Vector3[9]
            {
                new Vector3(-0.25f, 0f, -0.25f),
                new Vector3( 0.25f, 0f, -0.25f),
                new Vector3( 0.25f, 0f,  0.25f),
                new Vector3(-0.25f, 0f,  0.25f),
                new Vector3(-0.5f, 0f, 0f),
                new Vector3( 0.5f, 0f, 0f),
                new Vector3( 0f, 0f, -0.5f),
                new Vector3( 0f, 0f,  0.5f),
                new Vector3( 0f, 0f, 0f)
            };
            mesh.vertices = v;
            mesh.triangles = new int[]
            {
                0, 8, 1, 1, 8, 2, 2, 8, 3, 3, 8, 0,
                8, 4, 0, 8, 1, 5, 8, 2, 7, 8, 3, 4,
                0, 4, 6, 0, 6, 8
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh BuildAsymmetricOuterCorner()
        {
            Mesh mesh = new Mesh { name = "PrimAsymmetricOuterCorner" };
            float h = 0.5f;
            Vector3[] v = new Vector3[8];
            v[0] = new Vector3(-h, -h, -h);
            v[1] = new Vector3( h, -h, -h);
            v[2] = new Vector3( h, -h,  h);
            v[3] = new Vector3(-h, -h,  h);
            v[4] = new Vector3(-h,  h, -h);
            v[5] = new Vector3( h,  0, -h);
            v[6] = new Vector3( h,  0,  h);
            v[7] = new Vector3(-h,  0,  h);
            mesh.vertices = v;
            mesh.triangles = new int[]
            {
                0, 1, 2, 0, 2, 3,
                0, 4, 5, 0, 5, 1,
                1, 5, 6, 1, 6, 2,
                2, 6, 7, 2, 7, 3,
                3, 7, 4, 3, 4, 0,
                4, 5, 6, 4, 6, 7
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh BuildAsymmetricInnerCorner()
        {
            Mesh mesh = new Mesh { name = "PrimAsymmetricInnerCorner" };
            float h = 0.5f;
            Vector3[] v = new Vector3[7];
            v[0] = new Vector3(-h, -h, -h);
            v[1] = new Vector3( h, -h, -h);
            v[2] = new Vector3( h, -h,  h);
            v[3] = new Vector3(-h, -h,  h);
            v[4] = new Vector3( h,  h,  h);
            v[5] = new Vector3( h,  0, -h);
            v[6] = new Vector3(-h,  0,  h);
            mesh.vertices = v;
            mesh.triangles = new int[]
            {
                0, 1, 2, 0, 2, 3,
                1, 2, 4, 1, 4, 5,
                2, 3, 6, 2, 6, 4,
                3, 0, 6,
                0, 1, 5,
                4, 5, 0, 4, 0, 6
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh BuildHalfVerticalFront()
        {
            Mesh mesh = new Mesh { name = "PrimHalfVerticalFront" };
            float h = 0.5f;
            Vector3[] v = new Vector3[8];
            v[0] = new Vector3(-h, -h, -h);
            v[1] = new Vector3( h, -h, -h);
            v[2] = new Vector3( h, -h,  0);
            v[3] = new Vector3(-h, -h,  0);
            v[4] = new Vector3(-h,  h, -h);
            v[5] = new Vector3( h,  h, -h);
            v[6] = new Vector3( h,  h,  0);
            v[7] = new Vector3(-h,  h,  0);
            mesh.vertices = v;
            mesh.triangles = new int[]
            {
                0, 1, 2, 0, 2, 3,
                4, 7, 6, 4, 6, 5,
                0, 4, 5, 0, 5, 1,
                1, 5, 6, 1, 6, 2,
                2, 6, 7, 2, 7, 3,
                3, 7, 4, 3, 4, 0
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh BuildHalfVerticalBack()
        {
            Mesh mesh = new Mesh { name = "PrimHalfVerticalBack" };
            float h = 0.5f;
            Vector3[] v = new Vector3[8];
            v[0] = new Vector3(-h, -h,  0);
            v[1] = new Vector3( h, -h,  0);
            v[2] = new Vector3( h, -h,  h);
            v[3] = new Vector3(-h, -h,  h);
            v[4] = new Vector3(-h,  h,  0);
            v[5] = new Vector3( h,  h,  0);
            v[6] = new Vector3( h,  h,  h);
            v[7] = new Vector3(-h,  h,  h);
            mesh.vertices = v;
            mesh.triangles = new int[]
            {
                0, 1, 2, 0, 2, 3,
                4, 7, 6, 4, 6, 5,
                0, 4, 5, 0, 5, 1,
                1, 5, 6, 1, 6, 2,
                2, 6, 7, 2, 7, 3,
                3, 7, 4, 3, 4, 0
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh BuildHalfVerticalLeft()
        {
            Mesh mesh = new Mesh { name = "PrimHalfVerticalLeft" };
            float h = 0.5f;
            Vector3[] v = new Vector3[8];
            v[0] = new Vector3(-h, -h, -h);
            v[1] = new Vector3( 0, -h, -h);
            v[2] = new Vector3( 0, -h,  h);
            v[3] = new Vector3(-h, -h,  h);
            v[4] = new Vector3(-h,  h, -h);
            v[5] = new Vector3( 0,  h, -h);
            v[6] = new Vector3( 0,  h,  h);
            v[7] = new Vector3(-h,  h,  h);
            mesh.vertices = v;
            mesh.triangles = new int[]
            {
                0, 1, 2, 0, 2, 3,
                4, 7, 6, 4, 6, 5,
                0, 4, 5, 0, 5, 1,
                1, 5, 6, 1, 6, 2,
                2, 6, 7, 2, 7, 3,
                3, 7, 4, 3, 4, 0
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh BuildHalfVerticalRight()
        {
            Mesh mesh = new Mesh { name = "PrimHalfVerticalRight" };
            float h = 0.5f;
            Vector3[] v = new Vector3[8];
            v[0] = new Vector3( 0, -h, -h);
            v[1] = new Vector3( h, -h, -h);
            v[2] = new Vector3( h, -h,  h);
            v[3] = new Vector3( 0, -h,  h);
            v[4] = new Vector3( 0,  h, -h);
            v[5] = new Vector3( h,  h, -h);
            v[6] = new Vector3( h,  h,  h);
            v[7] = new Vector3( 0,  h,  h);
            mesh.vertices = v;
            mesh.triangles = new int[]
            {
                0, 1, 2, 0, 2, 3,
                4, 7, 6, 4, 6, 5,
                0, 4, 5, 0, 5, 1,
                1, 5, 6, 1, 6, 2,
                2, 6, 7, 2, 7, 3,
                3, 7, 4, 3, 4, 0
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh BuildRoundedEdge()
        {
            Mesh mesh = new Mesh { name = "PrimRoundedEdge" };
            float h = 0.5f;
            int segments = 8;
            int arcVerts = segments + 1;
            Vector3[] v = new Vector3[6 + arcVerts * 2];
            v[0] = new Vector3(-h, -h, -h);
            v[1] = new Vector3( h, -h, -h);
            v[2] = new Vector3( h, -h,  h);
            v[3] = new Vector3(-h, -h,  h);
            v[4] = new Vector3(-h,  h,  h);
            v[5] = new Vector3( h,  h,  h);
            for (int i = 0; i < arcVerts; i++)
            {
                float t = (float)i / segments * Mathf.PI * 0.5f;
                float y = h * Mathf.Sin(t);
                float z = -h * Mathf.Cos(t);
                v[6 + i] = new Vector3(-h, y, z);
                v[6 + arcVerts + i] = new Vector3( h, y, z);
            }
            mesh.vertices = v;
            var tri = new System.Collections.Generic.List<int>(90);
            tri.AddRange(new int[] { 0,1,2, 0,2,3, 3,2,5, 3,5,4 });
            tri.AddRange(new int[] { 4,14,23, 4,23,5, 0,6,15, 0,15,1 });
            tri.AddRange(new int[] { 0,14,6, 0,4,14, 0,3,4 });
            tri.AddRange(new int[] { 1,15,23, 1,23,5, 1,5,2 });
            for (int i = 0; i < segments; i++)
            {
                int a = 6 + i;
                int d = 6 + i + 1;
                int b = 6 + arcVerts + i;
                int c = 6 + arcVerts + i + 1;
                tri.Add(a); tri.Add(b); tri.Add(c);
                tri.Add(a); tri.Add(c); tri.Add(d);
            }
            mesh.triangles = tri.ToArray();
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
