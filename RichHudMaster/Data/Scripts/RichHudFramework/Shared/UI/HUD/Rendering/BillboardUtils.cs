using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace RichHudFramework
{
    namespace UI
    {
        namespace Rendering
        {
            public static class BillBoardUtils
            {
                /// <summary>
                /// Renders a polygon from a given set of unique vertex coordinates. Triangles are defined by their
                /// indices and the tex coords are parallel to the vertex list.
                /// </summary>
                public static void AddTriangles(List<int> indices, List<Vector3D> vertices, ref PolyMaterial mat)
                {
                    for (int i = 0; i < indices.Count; i += 3)
                    {
                        MyTransparentGeometry.AddTriangleBillboard
                        (
                            vertices[indices[i]],
                            vertices[indices[i + 1]],
                            vertices[indices[i + 2]],
                            Vector3.Zero, Vector3.Zero, Vector3.Zero,
                            mat.texCoords[indices[i]],
                            mat.texCoords[indices[i + 1]],
                            mat.texCoords[indices[i + 2]],
                            mat.textureID, 0,
                            Vector3D.Zero,
                            mat.bbColor,
                            BlendTypeEnum.PostPP
                        );
                    }
                }

                /// <summary>
                /// Adds a triangles in the given starting index range
                /// </summary>
                public static void AddTriangleRange(Vector2I range, List<int> indices, List<Vector3D> vertices, ref PolyMaterial mat)
                {
                    for (int i = range.X; i <= range.Y; i += 3)
                    {
                        MyTransparentGeometry.AddTriangleBillboard
                        (
                            vertices[indices[i]],
                            vertices[indices[i + 1]],
                            vertices[indices[i + 2]],
                            Vector3.Zero, Vector3.Zero, Vector3.Zero,
                            mat.texCoords[indices[i]],
                            mat.texCoords[indices[i + 1]],
                            mat.texCoords[indices[i + 2]],
                            mat.textureID, 0,
                            Vector3D.Zero,
                            mat.bbColor,
                            BlendTypeEnum.PostPP
                        );
                    }
                }

                /// <summary>
                /// Renders a polygon from a given set of unique vertex coordinates. Triangles are defined by their
                /// indices.
                /// </summary>
                public static void AddTriangles(List<int> indices, List<Vector3D> vertices, ref TriMaterial mat)
                {
                    for (int i = 0; i < indices.Count; i += 3)
                    {
                        MyTransparentGeometry.AddTriangleBillboard
                        (
                            vertices[indices[i]],
                            vertices[indices[i + 1]],
                            vertices[indices[i + 2]],
                            Vector3.Zero, Vector3.Zero, Vector3.Zero,
                            mat.texCoords.Point0,
                            mat.texCoords.Point1,
                            mat.texCoords.Point2,
                            mat.textureID, 0,
                            Vector3D.Zero,
                            mat.bbColor,
                            BlendTypeEnum.PostPP
                        );
                    }
                }

                /// <summary>
                /// Adds a triangle starting at the given index.
                /// </summary>
                public static void AddTriangle(int start, List<int> indices, List<Vector3D> vertices, ref TriMaterial mat)
                {
                    MyTransparentGeometry.AddTriangleBillboard
                    (
                        vertices[indices[start]],
                        vertices[indices[start + 1]],
                        vertices[indices[start + 2]],
                        Vector3.Zero, Vector3.Zero, Vector3.Zero,
                        mat.texCoords.Point0,
                        mat.texCoords.Point1,
                        mat.texCoords.Point2,
                        mat.textureID, 0,
                        Vector3D.Zero,
                        mat.bbColor,
                        BlendTypeEnum.PostPP
                    );
                }

                public static void AddTriangle(ref TriMaterial mat, ref MyQuadD quad)
                {
                    MyTransparentGeometry.AddTriangleBillboard
                    (
                        quad.Point0,
                        quad.Point1,
                        quad.Point2,
                        Vector3.Zero, Vector3.Zero, Vector3.Zero,
                        mat.texCoords.Point0,
                        mat.texCoords.Point1,
                        mat.texCoords.Point2,
                        mat.textureID, 0,
                        Vector3D.Zero,
                        mat.bbColor,
                        BlendTypeEnum.PostPP
                    );
                }

                public static void AddQuad(ref QuadMaterial mat, ref MyQuadD quad)
                {
                    MyTransparentGeometry.AddTriangleBillboard
                    (
                        quad.Point0,
                        quad.Point1,
                        quad.Point2,
                        Vector3.Zero, Vector3.Zero, Vector3.Zero,
                        mat.texCoords.Point0,
                        mat.texCoords.Point1,
                        mat.texCoords.Point2,
                        mat.textureID, 0,
                        Vector3D.Zero,
                        mat.bbColor,
                        BlendTypeEnum.PostPP
                    );

                    MyTransparentGeometry.AddTriangleBillboard
                    (
                        quad.Point0,
                        quad.Point2,
                        quad.Point3,
                        Vector3.Zero, Vector3.Zero, Vector3.Zero,
                        mat.texCoords.Point0,
                        mat.texCoords.Point2,
                        mat.texCoords.Point3,
                        mat.textureID, 0,
                        Vector3D.Zero,
                        mat.bbColor,
                        BlendTypeEnum.PostPP
                    );
                }

                public static void AddQuad(ref BoundedQuadMaterial mat, ref MyQuadD quad)
                {
                    MyTransparentGeometry.AddTriangleBillboard
                    (
                        quad.Point0,
                        quad.Point1,
                        quad.Point2,
                        Vector3.Zero, Vector3.Zero, Vector3.Zero,
                        mat.texBounds.Min,
                        (mat.texBounds.Min + new Vector2(0f, mat.texBounds.Size.Y)),
                        mat.texBounds.Max,
                        mat.textureID, 0,
                        Vector3D.Zero,
                        mat.bbColor,
                        BlendTypeEnum.PostPP
                    );

                    MyTransparentGeometry.AddTriangleBillboard
                    (
                        quad.Point0,
                        quad.Point2,
                        quad.Point3,
                        Vector3.Zero, Vector3.Zero, Vector3.Zero,
                        mat.texBounds.Min,
                        mat.texBounds.Max,
                        (mat.texBounds.Min + new Vector2(mat.texBounds.Size.X, 0f)),
                        mat.textureID, 0,
                        Vector3D.Zero,
                        mat.bbColor,
                        BlendTypeEnum.PostPP
                    );
                }

                /// <summary>
                /// Converts a color to its normalized linear RGB equivalent. Assumes additive blending
                /// with premultiplied alpha.
                /// </summary>
                public static Vector4 GetBillBoardBoardColor(Color color)
                {
                    float opacity = color.A / 255f;

                    color.R = (byte)(color.R * opacity);
                    color.G = (byte)(color.G * opacity);
                    color.B = (byte)(color.B * opacity);

                    return ((Vector4)color).ToLinearRGB();
                }
            }
        }
    }
}