using RichHudFramework.Internal;
using RichHudFramework.UI;
using RichHudFramework.UI.Server;

namespace RichHudFramework.Server
{
    public sealed partial class RichHudMaster
    {
        private void InitSettingsMenu()
        {
            demoCategory = new TerminalPageCategory() { Name = "Demo", Enabled = false };
            var debugCategory = RichHudDebug.GetDebugCategory();

            RichHudTerminal.Root.Enabled = true;
            RichHudTerminal.Root.AddRange(new IModRootMember[]
            {
                new RebindPage()
                {
                    Name = "Binds",
                    GroupContainer = { { MasterBinds.BindGroup, BindsConfig.DefaultBinds, true } }
                },
                demoCategory,
                debugCategory
            });

            _commands["toggleDebug"].CommandInvoked += x =>
            {
                demoCategory.Enabled = RichHudDebug.EnableDebug;
                debugCategory.Enabled = RichHudDebug.EnableDebug;
            };

            demoCategory.AddRange(new TerminalPageBase[]
            {
                new DemoPage() { Name = "UI Library" },
                new ControlPage()
                {
                    Name = "Term Controls",
                    CategoryContainer =
                    {
                        new ControlCategory()
                        {
                            HeaderText = "ControlCategory",
                            SubheaderText = "Contains terminal controls grouped into ControlTiles",
                            TileContainer =
                            {
                                new ControlTile()
                                {
                                    new TerminalCheckbox()
                                    {
                                        Name = "TerminalCheckbox",
                                        ControlChangedHandler = (obj, args) =>
                                        {
                                            var checkbox = obj as TerminalCheckbox;
                                            ExceptionHandler.SendChatMessage($"{checkbox.Name} = {checkbox.Value}");
                                        },
                                        ToolTip = "Sets a binary value. Fires an event when toggled."
                                    },
                                    new TerminalButton()
                                    {
                                        Name = "TerminalButton",
                                        ControlChangedHandler = (obj, args) =>
                                        {
                                            var btn = obj as TerminalButton;
                                            ExceptionHandler.SendChatMessage($"{btn.Name} pressed");
                                        },
                                        ToolTip = "Simple button. Fires an event when clicked."
                                    },
                                    new TerminalTextField()
                                    {
                                        Name = "TerminalTextField",
                                        ControlChangedHandler = (obj, args) =>
                                        {
                                            var field = obj as TerminalTextField;
                                            ExceptionHandler.SendChatMessage($"New text: {field.Value}");
                                        },
                                        ToolTip = "One-line text field"
                                    }
                                },
                                new ControlTile()
                                {
                                    new TerminalColorPicker()
                                    {
                                        Name = "TerminalColorPicker",
                                        ControlChangedHandler = (obj, args) =>
                                        {
                                            var picker = obj as TerminalColorPicker;
                                            ExceptionHandler.SendChatMessage($"Color = ({picker.Value.R}, {picker.Value.G}, {picker.Value.B})");
                                        },
                                        ToolTip = "Sets a simple 24-bit RGB color, 0-255/8-bits per channel."
                                    }
                                },
                                new ControlTile()
                                {
                                    new TerminalLabel()
                                    {
                                        Name = "TerminalLabel",
                                    },
                                    new TerminalDragBox()
                                    {
                                        Name = "TerminalDragBox",
                                        ControlChangedHandler = (obj, args) =>
                                        {
                                            var box = obj as TerminalDragBox;
                                            ExceptionHandler.SendChatMessage($"New box position: {box.Value}");
                                        },
                                        ToolTip = "Spawns a draggable window for setting fixed position on the HUD.\nUseful for user-configurable" +
                                        " UI layout."
                                    },
                                    new TerminalDropdown<float>()
                                    {
                                        Name = "TerminalDropdown",
                                        List =
                                        {
                                            { "Entry 1", 0f },
                                            { "Entry 2", 1f },
                                            { "Entry 3", 1f },
                                            { "Entry 4", 2f },
                                            { "Entry 5", 3f },
                                            { "Entry 6", 5f },
                                            { "Entry 7", 8f },
                                            { "Entry 8", 13f },
                                        },
                                        ControlChangedHandler = (obj, args) =>
                                        {
                                            var list = obj as TerminalDropdown<float>;
                                            ExceptionHandler.SendChatMessage($"Selected: {list.Value.Element.TextBoard} = {list.Value.AssocMember}");
                                        },
                                        ToolTip = "A generic dropdown list with custom labels associated with arbitrary values."
                                    }
                                }
                            }
                        },
                        new ControlCategory()
                        {
                            HeaderText = "ControlCategory",
                            SubheaderText = "Contains terminal controls grouped into ControlTiles",
                            TileContainer =
                            {
                                new ControlTile()
                                {
                                    new TerminalList<float>()
                                    {
                                        Name = "TerminalList",
                                        List =
                                        {
                                            { "Entry 1", 21f },
                                            { "Entry 2", 34f },
                                            { "Entry 3", 55f },
                                            { "Entry 4", 89f },
                                            { "Entry 5", 144f },
                                            { "Entry 6", 233f },
                                            { "Entry 7", 377f },
                                            { "Entry 8", 610f },
                                        },
                                        ControlChangedHandler = (obj, args) =>
                                        {
                                            var list = obj as TerminalList<float>;
                                            ExceptionHandler.SendChatMessage($"Selected: {list.Value.Element.TextBoard} = {list.Value.AssocMember}");
                                        },
                                        ToolTip = "Fixed-size scrolling list with custom labels associated with arbitrary values."
                                    }
                                },
                                new ControlTile()
                                {
                                    new TerminalOnOffButton()
                                    {
                                        Name = "TerminalOnOffButton",
                                        ControlChangedHandler = (obj, args) =>
                                        {
                                            var toggle = obj as TerminalOnOffButton;
                                            ExceptionHandler.SendChatMessage($"{toggle.Name} = {toggle.Value}");
                                        },
                                        ToolTip = "Alternative to checkbox. Functionally identical."
                                    },
                                    new TerminalSlider()
                                    {
                                        Name = "TerminalSlider",
                                        ValueText = "0",
                                        Min = 0f, Max = 100f, Value = 0f,
                                        ControlChangedHandler = (obj, args) =>
                                        {
                                            var slider = obj as TerminalSlider;
                                            slider.ValueText = $"{slider.Value:G4}";
                                            ExceptionHandler.SendChatMessage($"Slider value changed: {slider.Value:G3}");
                                        },
                                        ToolTip = "Slider based on 32-bit float value with customizable value text."
                                    },
                                },
                            }
                        },
                    }
                }
            }); ;
        }
    }
}