using Sandbox.ModAPI;
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
                public FlatQuad matFit;

                /// <summary>
                /// Determines the extent to which the quad will be rhombused
                /// </summary>
                public float skewRatio;

                static QuadBoard()
                {
                    var matFit = new FlatQuad(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));
                    Default = new QuadBoard(Material.Default.TextureID, matFit, Color.White);
                }

                public QuadBoard(MyStringId textureID, FlatQuad matFit, Vector4 bbColor, float skewRatio = 0f)
                {
                    this.textureID = textureID;
                    this.matFit = matFit;
                    this.bbColor = bbColor;
                    this.skewRatio = skewRatio;
                }

                public QuadBoard(MyStringId textureID, FlatQuad matFit, Color color, float skewRatio = 0f)
                {
                    this.textureID = textureID;
                    this.matFit = matFit;
                    bbColor = GetQuadBoardColor(color);
                    this.skewRatio = skewRatio;
                }

                /// <summary>
                /// Draws a billboard in world space using the quad specified.
                /// </summary>
                public void Draw(ref MyQuadD quad)
                {
                    AddBillboard(ref quad, textureID, ref matFit, bbColor);
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

                    AddBillboard(ref quad, textureID, ref matFit, bbColor);
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

                    AddBillboard(ref quad, textureID, ref matFit, bbColor);
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

                    AddBillboard(ref quad, textureID, ref matFit, bbColor);
                }

                /// <summary>
                /// Draws a cropped billboard in world space facing the +Z direction of the matrix specified. Cropping is 
                /// performed s.t. any parts outside the box defined by maskMin and maskMax are not rendered. For 
                /// NON-TEXTURED billboards ONLY. This method will warp textures. Units in meters, matrix transform 
                /// notwithstanding.
                /// </summary>
                public void DrawCropped(Vector2 size, Vector2 origin, Vector2 maskMin, Vector2 maskMax, ref MatrixD matrix)
                {
                    // Calculate the position of the -/+ bounds of the box
                    Vector2 minBound = Vector2.Max(origin - size * .5f, maskMin),
                        maxBound = Vector2.Min(origin + size * .5f, maskMax);

                    // Adjust size and offset to simulate clipping
                    size = Vector2.Max(maxBound - minBound, Vector2.Zero);

                    if ((size.X + size.Y) > 1E-3)
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

                        AddBillboard(ref quad, textureID, ref matFit, bbColor);
                    }
                }

                /// <summary>
                /// Draws a cropped billboard in world space facing the +Z direction of the matrix specified. Cropping is 
                /// performed s.t. any parts outside the box defined by maskMin and maskMax are not rendered and WITHOUT 
                /// warping the texture or displacing the billboard. Units in meters, matrix transform notwithstanding.
                /// </summary>
                public void DrawCroppedTex(Vector2 size, Vector2 origin, Vector2 maskMin, Vector2 maskMax, ref MatrixD matrix)
                {
                    // Calculate the position of the -/+ bounds of the box
                    Vector2 minBound = Vector2.Max(origin - size * .5f, maskMin),
                        maxBound = Vector2.Min(origin + size * .5f, maskMax);
                    // Cropped dimensions
                    Vector2 clipSize = Vector2.Max(maxBound - minBound, Vector2.Zero);

                    if ((clipSize.X * clipSize.Y) > 1E-10)
                    {
                        FlatQuad texCoords = matFit;
                        // Normalized cropped size and offset
                        Vector2 clipScale = clipSize / size,
                            clipOffset = (.5f * (maxBound + minBound) - origin) / size;

                        if ((clipScale.X * clipScale.Y) > 1E-3)
                        {
                            Vector2 uvScale = texCoords.Point2 - texCoords.Point0,
                                uvOffset = .5f * (texCoords.Point2 + texCoords.Point0);

                            origin += clipOffset * size; // Offset billboard to compensate for changes in size
                            size *= clipScale; // Calculate final billboard size
                            clipOffset *= uvScale * new Vector2(1f, -1f); // Scale offset to fit material and flip Y-axis

                            // Recalculate texture coordinates to simulate clipping without affecting material alignment
                            texCoords.Point0 = ((texCoords.Point0 - uvOffset) * clipScale) + uvOffset + clipOffset;
                            texCoords.Point1 = ((texCoords.Point1 - uvOffset) * clipScale) + uvOffset + clipOffset;
                            texCoords.Point2 = ((texCoords.Point2 - uvOffset) * clipScale) + uvOffset + clipOffset;
                            texCoords.Point3 = ((texCoords.Point3 - uvOffset) * clipScale) + uvOffset + clipOffset;
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

                        AddBillboard(ref quad, textureID, ref texCoords, bbColor);
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

                private static void AddBillboard(ref MyQuadD quad, MyStringId matID, ref FlatQuad matFit, Vector4 color)
                {
                    MyTransparentGeometry.AddTriangleBillboard
                    (
                        quad.Point0,
                        quad.Point1,
                        quad.Point2,
                        Vector3.Zero, Vector3.Zero, Vector3.Zero,
                        matFit.Point0,
                        matFit.Point1,
                        matFit.Point2,
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
                        matFit.Point0,
                        matFit.Point2,
                        matFit.Point3,
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