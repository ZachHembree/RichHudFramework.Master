﻿using Sandbox.ModAPI;
using VRage.Game;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace RichHudFramework
{
    namespace UI
    {
        using Client;
        using Server;

        namespace Rendering
        {
            /// <summary>
            /// Defines a rectangular billboard drawn on the HUD using a material with texture coordinates
            /// accessible for each vertex using a FlatQuad.
            /// </summary>
            public struct QuadBoard
            {
                public static readonly QuadBoard Default;

                /// <summary>
                /// Determines the extent to which the quad will be rhombused
                /// </summary>
                public float skewRatio;

                /// <summary>
                /// Material ID used by the billboard.
                /// </summary>
                public MyStringId textureID;

                /// <summary>
                /// Color of the billboard using native formatting
                /// </summary>
                public Vector4 bbColor;

                /// <summary>
                /// Determines the scale and aspect ratio of the texture as rendered.
                /// </summary>
                public BoundingBox2 texCoords;

                static QuadBoard()
                {
                    var matFit = new BoundingBox2(new Vector2(0f, 0f), new Vector2(0f, 1f));
                    Default = new QuadBoard(Material.Default.TextureID, matFit, Color.White);
                }

                public QuadBoard(MyStringId textureID, BoundingBox2 matFit, Vector4 bbColor, float skewRatio = 0f)
                {
                    this.textureID = textureID;
                    this.texCoords = matFit;
                    this.bbColor = bbColor;
                    this.skewRatio = skewRatio;
                }

                public QuadBoard(MyStringId textureID, BoundingBox2 matFit, Color color, float skewRatio = 0f)
                {
                    this.textureID = textureID;
                    this.texCoords = matFit;
                    bbColor = GetQuadBoardColor(color);
                    this.skewRatio = skewRatio;
                }

                /// <summary>
                /// Draws a billboard in world space using the quad specified.
                /// </summary>
                public void Draw(ref MyQuadD quad)
                {
                    AddBillboard(ref quad, textureID, texCoords, bbColor);
                }

                /// <summary>
                /// Draws a billboard in world space facing the +Z direction of the matrix specified. Units in meters matrix
                /// transform notwithstanding.
                /// </summary>
                public void Draw(Vector2 size, Vector3D origin, ref MatrixD matrix)
                {
                    MyQuadD quad;

                    Vector3D.TransformNoProjection(ref origin, ref matrix, out origin);
                    MyUtils.GenerateQuad(out quad, ref origin, size.X * .5f, size.Y * .5f, ref matrix);

                    if (skewRatio != 0f)
                    {
                        Vector3D start = quad.Point0, end = quad.Point3,
                            offset = (end - start) * skewRatio * .5;

                        quad.Point0 = Vector3D.Lerp(start, end, skewRatio) - offset;
                        quad.Point3 = Vector3D.Lerp(start, end, 1d + skewRatio) - offset;
                        quad.Point1 -= offset;
                        quad.Point2 -= offset;
                    }

                    AddBillboard(ref quad, textureID, texCoords, bbColor);
                }

                /// <summary>
                /// Draws a billboard in world space facing the +Z direction of the matrix specified. Units in meters, matrix
                /// transform notwithstanding.
                /// </summary>
                public void Draw(Vector2 size, Vector2 origin, ref MatrixD matrix)
                {
                    Vector3D worldPos = new Vector3D(origin.X, origin.Y, 0d);
                    MyQuadD quad;

                    Vector3D.TransformNoProjection(ref worldPos, ref matrix, out worldPos);
                    MyUtils.GenerateQuad(out quad, ref worldPos, size.X * .5f, size.Y * .5f, ref matrix);

                    if (skewRatio != 0f)
                    {
                        Vector3D start = quad.Point0, end = quad.Point3,
                            offset = (end - start) * skewRatio * .5;

                        quad.Point0 = Vector3D.Lerp(start, end, skewRatio) - offset;
                        quad.Point3 = Vector3D.Lerp(start, end, 1d + skewRatio) - offset;
                        quad.Point1 -= offset;
                        quad.Point2 -= offset;
                    }

                    AddBillboard(ref quad, textureID, texCoords, bbColor);
                }

                /// <summary>
                /// Draws billboard in screen space with a given size in pixels at a given origin in pixels.
                /// </summary>
                public void Draw(Vector2 size, Vector2 origin)
                {
                    MatrixD ptw = HudMain.PixelToWorld;
                    Vector3D worldPos = new Vector3D(origin.X, origin.Y, 0d);
                    MyQuadD quad;

                    Vector3D.TransformNoProjection(ref worldPos, ref ptw, out worldPos);
                    MyUtils.GenerateQuad(out quad, ref worldPos, size.X * .5f, size.Y * .5f, ref ptw);

                    if (skewRatio != 0f)
                    {
                        Vector3D start = quad.Point0, end = quad.Point3,
                            offset = (end - start) * skewRatio * .5;

                        quad.Point0 = Vector3D.Lerp(start, end, skewRatio) - offset;
                        quad.Point3 = Vector3D.Lerp(start, end, 1d + skewRatio) - offset;
                        quad.Point1 -= offset;
                        quad.Point2 -= offset;
                    }

                    AddBillboard(ref quad, textureID, texCoords, bbColor);
                }

                /// <summary>
                /// Draws a cropped billboard in world space facing the +Z direction of the matrix specified. Cropping is 
                /// performed s.t. any parts outside the box defined by maskMin and maskMax are not rendered. For 
                /// NON-TEXTURED billboards ONLY. This method will warp textures. Units in meters, matrix transform 
                /// notwithstanding.
                /// </summary>
                public void DrawCropped(Vector2 size, Vector2 origin, BoundingBox2 mask, ref MatrixD matrix)
                {
                    // Calculate the position of the -/+ bounds of the box
                    Vector2 minBound = Vector2.Max(origin - size * .5f, mask.Min),
                        maxBound = Vector2.Min(origin + size * .5f, mask.Max);

                    // Adjust size and offset to simulate clipping
                    size = Vector2.Max(maxBound - minBound, Vector2.Zero);

                    if ((size.X * size.Y) > 1E-6)
                    {
                        origin = (maxBound + minBound) * .5f;

                        Vector3D worldPos = new Vector3D(origin.X, origin.Y, 0d);
                        MyQuadD quad;

                        Vector3D.TransformNoProjection(ref worldPos, ref matrix, out worldPos);
                        MyUtils.GenerateQuad(out quad, ref worldPos, size.X * .5f, size.Y * .5f, ref matrix);

                        if (skewRatio != 0f)
                        {
                            Vector3D start = quad.Point0, end = quad.Point3,
                                offset = (end - start) * skewRatio * .5;

                            quad.Point0 = Vector3D.Lerp(start, end, skewRatio) - offset;
                            quad.Point3 = Vector3D.Lerp(start, end, 1d + skewRatio) - offset;
                            quad.Point1 -= offset;
                            quad.Point2 -= offset;
                        }

                        AddBillboard(ref quad, textureID, texCoords, bbColor);
                    }
                }

                /// <summary>
                /// Draws a cropped billboard in world space facing the +Z direction of the matrix specified. Cropping is 
                /// performed s.t. any parts outside the box defined by maskMin and maskMax are not rendered and WITHOUT 
                /// warping the texture or displacing the billboard. Units in meters, matrix transform notwithstanding.
                /// </summary>
                public void DrawCroppedTex(Vector2 size, Vector2 origin, BoundingBox2 mask, ref MatrixD matrix)
                {
                    // Calculate the position of the -/+ bounds of the box
                    Vector2 minBound = Vector2.Max(origin - size * .5f, mask.Min),
                        maxBound = Vector2.Min(origin + size * .5f, mask.Max);
                    // Cropped dimensions
                    Vector2 clipSize = Vector2.Max(maxBound - minBound, Vector2.Zero);

                    if ((clipSize.X * clipSize.Y) > 1E-6)
                    {
                        BoundingBox2 tc = texCoords;                        

                        if ((clipSize - size).LengthSquared() > 1E-3)
                        {
                            // Normalized cropped size and offset
                            Vector2 clipScale = clipSize / size,
                                clipOffset = (.5f * (maxBound + minBound) - origin) / size,
                                uvScale = tc.Size,
                                uvOffset = tc.Center;

                            origin += clipOffset * size; // Offset billboard to compensate for changes in size
                            size *= clipScale; // Calculate final billboard size
                            clipOffset *= uvScale * new Vector2(1f, -1f); // Scale offset to fit material and flip Y-axis

                            // Recalculate texture coordinates to simulate clipping without affecting material alignment
                            tc.Min = ((tc.Min - uvOffset) * clipScale) + uvOffset + clipOffset;
                            tc.Max = ((tc.Max - uvOffset) * clipScale) + uvOffset + clipOffset;
                        }

                        Vector3D worldPos = new Vector3D(origin.X, origin.Y, 0d);
                        MyQuadD quad;

                        Vector3D.TransformNoProjection(ref worldPos, ref matrix, out worldPos);
                        MyUtils.GenerateQuad(out quad, ref worldPos, size.X * .5f, size.Y * .5f, ref matrix);

                        if (skewRatio != 0f)
                        {
                            Vector3D start = quad.Point0, end = quad.Point3,
                                offset = (end - start) * skewRatio * .5;

                            quad.Point0 = Vector3D.Lerp(start, end, skewRatio) - offset;
                            quad.Point3 = Vector3D.Lerp(start, end, 1d + skewRatio) - offset;
                            quad.Point1 -= offset;
                            quad.Point2 -= offset;
                        }

                        AddBillboard(ref quad, textureID, tc, bbColor);
                    }
                }

                public static Vector4 GetQuadBoardColor(Color color)
                {   
                    float opacity = color.A / 255f;

                    color.R = (byte)(color.R * opacity);
                    color.G = (byte)(color.G * opacity);
                    color.B = (byte)(color.B * opacity);

                    return ((Vector4)color).ToLinearRGB();
                }

                private static void AddBillboard(ref MyQuadD quad, MyStringId matID, BoundingBox2 matFit, Vector4 color)
                {
                    MyTransparentGeometry.AddTriangleBillboard
                    (
                        quad.Point0,
                        quad.Point1,
                        quad.Point2,
                        Vector3.Zero, Vector3.Zero, Vector3.Zero,
                        matFit.Min,
                        (matFit.Min + new Vector2(0f, matFit.Size.Y)),
                        matFit.Max,
                        matID, 0,
                        Vector3D.Zero,
                        color,
                        BlendTypeEnum.PostPP
                    );

                    MyTransparentGeometry.AddTriangleBillboard
                    (
                        quad.Point0,
                        quad.Point2,
                        quad.Point3,
                        Vector3.Zero, Vector3.Zero, Vector3.Zero,
                        matFit.Min,
                        matFit.Max,
                        (matFit.Min + new Vector2(matFit.Size.X, 0f)),
                        matID, 0,
                        Vector3D.Zero,
                        color,
                        BlendTypeEnum.PostPP
                    );
                }
            }

        }
    }
}