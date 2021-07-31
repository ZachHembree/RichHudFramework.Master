using VRageMath;

namespace RichHudFramework
{
    namespace UI
    {
        using Client;

        namespace Rendering
        {
            public class MatBoard
            {
                /// <summary>
                /// Coloring applied to the material.
                /// </summary>
                public Color Color
                {
                    get { return color; }
                    set
                    {
                        if (value != color)
                            minBoard.bbColor = QuadBoard.GetQuadBoardColor(value);

                        color = value;
                    }
                }

                /// <summary>
                /// Texture applied to the billboard.
                /// </summary>
                public Material Material
                {
                    get { return matFrame.Material; }
                    set
                    {
                        if (value != matFrame.Material)
                        {
                            updateMatFit = true;
                            matFrame.Material = value;
                            minBoard.textureID = value.TextureID;
                        }
                    }
                }

                /// <summary>
                /// Determines how the texture scales with the MatBoard's dimensions.
                /// </summary>
                public MaterialAlignment MatAlignment
                {
                    get { return matFrame.Alignment; }
                    set
                    {
                        if (value != matFrame.Alignment)
                        {
                            updateMatFit = true;
                            matFrame.Alignment = value;
                        }
                    }
                }

                private Color color;
                private bool updateMatFit;

                private QuadBoard minBoard;
                private readonly MaterialFrame matFrame;

                /// <summary>
                /// Initializes a new matboard with a size of 0 and a blank, white material.
                /// </summary>
                public MatBoard()
                {
                    matFrame = new MaterialFrame();
                    minBoard = QuadBoard.Default;

                    color = Color.White;
                    updateMatFit = true;
                }

                /// <summary>
                /// Draws a billboard in world space using the quad specified.
                /// </summary>
                public void Draw(ref MyQuadD quad)
                {
                    minBoard.Draw(ref quad);
                }

                /// <summary>
                /// Draws a billboard in world space facing the +Z direction of the matrix specified. Units in meters matrix
                /// transform notwithstanding.
                /// </summary>
                public void Draw(Vector2 size, Vector3D origin, ref MatrixD matrix)
                {
                    if (updateMatFit && matFrame.Material != Material.Default)
                    {
                        minBoard.texCoords = matFrame.GetMaterialAlignment(size.X / size.Y);
                        updateMatFit = false;
                    }

                    minBoard.Draw(size, origin, ref matrix);
                }

                /// <summary>
                /// Draws a cropped billboard in world space facing the +Z direction of the matrix specified. Cropping is 
                /// performed s.t. any parts outside the box defined by maskMin and maskMax are not rendered. For 
                /// NON-TEXTURED billboards ONLY. This method will warp textures. Units in meters, matrix transform 
                /// notwithstanding.
                /// </summary>
                public void DrawCropped(ref CroppedBox box, ref MatrixD matrix)
                {
                    if (updateMatFit && matFrame.Material != Material.Default)
                    {
                        minBoard.texCoords = matFrame.GetMaterialAlignment(box.size.X / box.size.Y);
                        updateMatFit = false;
                    }

                    minBoard.DrawCropped(ref box, ref matrix);
                }

                /// <summary>
                /// Draws a cropped billboard in world space facing the +Z direction of the matrix specified. Cropping is 
                /// performed s.t. any parts outside the box defined by maskMin and maskMax are not rendered. For 
                /// NON-TEXTURED billboards ONLY. This method will warp textures. Units in meters, matrix transform 
                /// notwithstanding.
                /// </summary>
                public void DrawCroppedTex(ref CroppedBox box, ref MatrixD matrix)
                {
                    if (updateMatFit && matFrame.Material != Material.Default)
                    {
                        minBoard.texCoords = matFrame.GetMaterialAlignment(box.size.X / box.size.Y);
                        updateMatFit = false;
                    }

                    minBoard.DrawCroppedTex(ref box, ref matrix);
                }

                /// <summary>
                /// Draws a billboard in world space facing the +Z direction of the matrix given. Units in meters,
                /// matrix transform notwithstanding. Dont forget to compensate for perspective scaling!
                /// </summary>
                public void Draw(ref CroppedBox box, ref MatrixD matrix)
                {
                    if (updateMatFit && matFrame.Material != Material.Default)
                    {
                        minBoard.texCoords = matFrame.GetMaterialAlignment(box.size.X / box.size.Y);
                        updateMatFit = false;
                    }

                    minBoard.Draw(ref box, ref matrix);
                }       
            }
        }
    }
}