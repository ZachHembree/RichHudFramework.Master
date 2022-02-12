﻿using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Utils;
using VRageMath;
using VRageRender;
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
            /// Bounding box paired with another as a mask for clipping billboards
            /// </summary>
            public struct CroppedBox
            {
                public static readonly BoundingBox2 defaultMask =
                    new BoundingBox2(-Vector2.PositiveInfinity, Vector2.PositiveInfinity);

                public BoundingBox2 bounds;
                public BoundingBox2? mask;
            }

            /// <summary>
            /// Final 3D quad for <see cref="QuadBoard"/> generated prior to rendering
            /// </summary>
            public struct QuadBoardData
            {
                public BoundedQuadMaterial material;
                public MyQuadD positions;
            }

            /// <summary>
            /// <see cref="QuadBoard"/> with bounding
            /// </summary>
            public struct BoundedQuadBoard
            {
                public BoundingBox2 bounds;
                public QuadBoard quadBoard;
            }

            /// <summary>
            /// Defines a rectangular billboard with texture coordinates defined by a bounding box
            /// </summary>
            public struct QuadBoard
            {
                public static readonly QuadBoard Default;

                /// <summary>
                /// Determines the extent to which the quad will be rhombused
                /// </summary>
                public float skewRatio;

                /// <summary>
                /// Determines material applied to the billboard as well as its alignment, bounding and tint
                /// </summary>
                public BoundedQuadMaterial materialData;

                static QuadBoard()
                {
                    var matFit = new BoundingBox2(new Vector2(0f, 0f), new Vector2(1f, 1f));
                    Default = new QuadBoard(Material.Default.TextureID, matFit, Color.White);
                }

                public QuadBoard(MyStringId textureID, BoundingBox2 matFit, Vector4 bbColor, float skewRatio = 0f)
                {
                    materialData.textureID = textureID;
                    materialData.texBounds = matFit;
                    materialData.bbColor = bbColor;
                    this.skewRatio = skewRatio;
                }

                public QuadBoard(MyStringId textureID, BoundingBox2 matFit, Color color, float skewRatio = 0f)
                {
                    materialData.textureID = textureID;
                    materialData.texBounds = matFit;
                    materialData.bbColor = BillBoardUtils.GetBillBoardBoardColor(color);
                    this.skewRatio = skewRatio;
                }

                /// <summary>
                /// Draws a billboard in world space using the quad specified.
                /// </summary>
                [Obsolete]
                public void Draw(ref MyQuadD quad)
                { }

                /// <summary>
                /// Draws a billboard in world space facing the +Z direction of the matrix specified. Units in meters, matrix
                /// transform notwithstanding.
                /// </summary>
                public void Draw(ref CroppedBox box, MatrixD[] matrixRef)
                {
                    FlatQuad quad = new FlatQuad()
                    {
                        Point0 = box.bounds.Max,
                        Point1 = new Vector2(box.bounds.Max.X, box.bounds.Min.Y),
                        Point2 = box.bounds.Min,
                        Point3 = new Vector2(box.bounds.Min.X, box.bounds.Max.Y),
                    };

                    if (skewRatio != 0f)
                    {
                        Vector2 start = quad.Point0, end = quad.Point3,
                            offset = (end - start) * skewRatio * .5f;

                        quad.Point0 = Vector2.Lerp(start, end, skewRatio) - offset;
                        quad.Point3 = Vector2.Lerp(start, end, 1f + skewRatio) - offset;
                        quad.Point1 -= offset;
                        quad.Point2 -= offset;
                    }

                    BillBoardUtils.AddQuad(ref quad, ref materialData, matrixRef);
                }

                /// <summary>
                /// Draws a cropped billboard in world space facing the +Z direction of the matrix specified. Cropping is 
                /// performed s.t. any parts outside the box defined by maskMin and maskMax are not rendered. For 
                /// NON-TEXTURED billboards ONLY. This method will warp textures. Units in meters, matrix transform 
                /// notwithstanding.
                /// </summary>
                public void DrawCropped(ref CroppedBox box, MatrixD[] matrixRef)
                {
                    box.bounds = box.bounds.Intersect(box.mask.Value);

                    // Generate quad from clipped bounds
                    FlatQuad quad = new FlatQuad()
                    {
                        Point0 = box.bounds.Max,
                        Point1 = new Vector2(box.bounds.Max.X, box.bounds.Min.Y),
                        Point2 = box.bounds.Min,
                        Point3 = new Vector2(box.bounds.Min.X, box.bounds.Max.Y),
                    };

                    if (skewRatio != 0f)
                    {
                        Vector2 start = quad.Point0, end = quad.Point3,
                            offset = (end - start) * skewRatio * .5f;

                        quad.Point0 = Vector2.Lerp(start, end, skewRatio) - offset;
                        quad.Point3 = Vector2.Lerp(start, end, 1f + skewRatio) - offset;
                        quad.Point1 -= offset;
                        quad.Point2 -= offset;
                    }

                    BillBoardUtils.AddQuad(ref quad, ref materialData, matrixRef);
                }

                /// <summary>
                /// Draws a cropped billboard in world space facing the +Z direction of the matrix specified. Cropping is 
                /// performed s.t. any parts outside the box defined by maskMin and maskMax are not rendered and WITHOUT 
                /// warping the texture or displacing the billboard. Units in meters, matrix transform notwithstanding.
                /// </summary>
                public void DrawCroppedTex(ref CroppedBox box, MatrixD[] matrixRef)
                {
                    Vector2 size = box.bounds.Size,
                        pos = box.bounds.Center;
                    box.bounds = box.bounds.Intersect(box.mask.Value);

                    Vector2 clipSize = box.bounds.Size;
                    BoundedQuadMaterial cropMat = materialData;

                    // Normalized cropped size and offset
                    Vector2 clipScale = clipSize / size,
                        clipOffset = (box.bounds.Center - pos) / size,
                        uvScale = cropMat.texBounds.Size,
                        uvOffset = cropMat.texBounds.Center;

                    clipOffset *= uvScale * new Vector2(1f, -1f); // Scale offset to fit material and flip Y-axis

                    // Recalculate texture coordinates to simulate clipping without affecting material alignment
                    cropMat.texBounds.Min = ((cropMat.texBounds.Min - uvOffset) * clipScale) + (uvOffset + clipOffset);
                    cropMat.texBounds.Max = ((cropMat.texBounds.Max - uvOffset) * clipScale) + (uvOffset + clipOffset);

                    FlatQuad quad = new FlatQuad() 
                    {
                        Point0 = box.bounds.Max,
                        Point1 = new Vector2(box.bounds.Max.X, box.bounds.Min.Y),
                        Point2 = box.bounds.Min,
                        Point3 = new Vector2(box.bounds.Min.X, box.bounds.Max.Y),
                    };

                    if (skewRatio != 0f)
                    {
                        Vector2 start = quad.Point0, end = quad.Point3,
                            offset = (end - start) * skewRatio * .5f;

                        quad.Point0 = Vector2.Lerp(start, end, skewRatio) - offset;
                        quad.Point3 = Vector2.Lerp(start, end, 1f + skewRatio) - offset;
                        quad.Point1 -= offset;
                        quad.Point2 -= offset;
                    }

                    BillBoardUtils.AddQuad(ref quad, ref cropMat, matrixRef);
                }
            }
        }
    }
}