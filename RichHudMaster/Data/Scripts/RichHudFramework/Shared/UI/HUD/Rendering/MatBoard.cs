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

                public Vector2 Size
                {
                    get { return size; }
                    set
                    {
                        if (value != size)
                            updateMatFit = true;

                        size = value;
                    }
                }

                public float Width
                {
                    get { return size.X; }
                    set
                    {
                        if (value != size.X)
                            updateMatFit = true;

                        size.X = value;
                    }
                }

                public float Height
                {
                    get { return size.Y; }
                    set
                    {
                        if (value != size.Y)
                            updateMatFit = true;

                        size.Y = value;
                    }
                }

                public Vector2 MatOffset
                {
                    get { return matFrame.offset; }
                    set
                    {
                        if (value != matFrame.offset)
                            updateMatFit = true;

                        matFrame.offset = value;
                    }
                }

                public float MatScale
                {
                    get { return matFrame.scale; }
                    set
                    {
                        if (value != matFrame.scale)
                            updateMatFit = true;

                        matFrame.scale = value;
                    }
                }

                public Material Material
                {
                    get { return matFrame.material; }
                    set
                    {
                        if (value != matFrame.material)
                            updateMatFit = true;

                        matFrame.material = value;
                        minBoard.textureID = value.TextureID;
                    }
                }

                public MaterialAlignment MatAlignment
                {
                    get { return matFrame.alignment; }
                    set
                    {
                        if (value != matFrame.alignment)
                            updateMatFit = true;

                        matFrame.alignment = value;
                    }
                }

                public Vector2 offset;

                private Vector2 size;
                private Color color;
                private bool updateMatFit;

                private QuadBoard minBoard;
                private MaterialFrame matFrame;

                public MatBoard()
                {
                    minBoard = new QuadBoard();
                    matFrame = new MaterialFrame();

                    Material = Material.Default;
                    Color = Color.White;
                    updateMatFit = true;
                }

                public void Draw(Vector2 origin)
                {
                    if (updateMatFit)
                    {
                        minBoard.matFit = matFrame.GetMaterialAlignment(Size);
                        updateMatFit = false;
                    }

                    minBoard.Draw(size, origin + offset);
                }           
            }

        }
    }
}