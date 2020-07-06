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

                public QuadBoard(MyStringId textureID, FlatQuad matFit, Vector4 bbColor)
                {
                    this.textureID = textureID;
                    this.matFit = matFit;
                    this.bbColor = bbColor;
                }

                public QuadBoard(MyStringId textureID, FlatQuad matFit, Color color)
                {
                    this.textureID = textureID;
                    this.matFit = matFit;
                    bbColor = GetQuadBoardColor(color);
                }

                /// <summary>
                /// Draws a billboard in world space using the quad specified.
                /// </summary>
                public void Draw(ref MyQuadD quad)
                {
                    AddBillboard(ref quad, textureID, ref matFit, bbColor);
                }

                /// <summary>
                /// Draws a billboard in world space facing the +Z direction the matrix specified. Units in meters matrix
                /// transform notwithstanding.
                /// </summary>
                public void Draw(Vector2 size, Vector3D origin, ref MatrixD matrix)
                {
                    MyQuadD quad;

                    Vector3D.Transform(ref origin, ref matrix, out origin);
                    MyUtils.GenerateQuad(out quad, ref origin, size.X / 2f, size.Y / 2f, ref matrix);

                    AddBillboard(ref quad, textureID, ref matFit, bbColor);
                }

                /// <summary>
                /// Draws a billboard in world space facing the +Z direction the matrix specified. Units in meters, matrix
                /// transform notwithstanding.
                /// </summary>
                public void Draw(Vector2 size, Vector2 origin, ref MatrixD matrix)
                {
                    Vector3D worldPos = new Vector3D(origin.X, origin.Y, 1d);
                    MyQuadD quad;

                    Vector3D.Transform(ref worldPos, ref matrix, out worldPos);
                    MyUtils.GenerateQuad(out quad, ref worldPos, size.X / 2f, size.Y / 2f, ref matrix);

                    AddBillboard(ref quad, textureID, ref matFit, bbColor);
                }

                /// <summary>
                /// Draws billboard in screen space with a given size in pixels at a given origin in pixels.
                /// </summary>
                public void Draw(Vector2 size, Vector2 origin)
                {
                    MatrixD ptw = HudMain.PixelToWorld;
                    Vector3D worldPos = new Vector3D(origin.X, origin.Y, 1d);
                    MyQuadD quad;

                    Vector3D.Transform(ref worldPos, ref ptw, out worldPos);
                    MyUtils.GenerateQuad(out quad, ref worldPos, size.X / 2f, size.Y / 2f, ref ptw);

                    AddBillboard(ref quad, textureID, ref matFit, bbColor);
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